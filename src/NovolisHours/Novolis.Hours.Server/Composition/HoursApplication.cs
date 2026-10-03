using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Novolis.Hours.Application;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Security.Authentication;
using Novolis.Security.Authentication.Storage;
using Novolis.Security.HaveIBeenPwned;
using Novolis.Storage.Abstractions;
using Novolis.Storage.InMemory;
using Novolis.Time.Worktime.Legal;
using Novolis.WorkflowEngine;
using AuthenticationFacade = Novolis.Security.Authentication.IAuthenticationService;

namespace Novolis.Hours.Server;

/// <summary>Composes the self-contained Novolis Hours server, JSON store, SPA, API, security, workflows, and realtime transport.</summary>
public static class HoursApplication
{
    /// <summary>Builds a complete host and exposes a builder hook for the no-mock TestServer harness.</summary>
    public static WebApplication Build(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null,
        string? environmentName = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(HoursApplication).Assembly.FullName,
            EnvironmentName = environmentName,
            ContentRootPath = AppContext.BaseDirectory,
        });
        configureBuilder?.Invoke(builder);

        var options = builder.Configuration.GetSection("Hours").Get<HoursServerOptions>() ?? new HoursServerOptions();
        if (!builder.Environment.IsDevelopment() && options.EnableDemoAdminCredentials)
        {
            throw new InvalidOperationException(
                "Hours__EnableDemoAdminCredentials is development-only and cannot be enabled outside Development.");
        }

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        ConfigureStorage(builder.Services, options);
        if (!builder.Environment.IsDevelopment() && !options.EnableDemoAdminCredentials)
        {
            builder.Services.AddNovolisPasswordBreachCheck();
        }
        builder.Services.AddNovolisAuthentication(authentication =>
        {
            authentication.IsDevelopment = builder.Environment.IsDevelopment() || options.EnableDemoAdminCredentials;
            authentication.ForbiddenPasswordFragments = ["novolis", "hours"];
        });
        builder.Services.AddNovolisAuthenticationStorage();
        builder.Services.AddSingleton<HoursUserDirectory>();
        builder.Services.AddSingleton<HoursHtmlReportExporter>();
        builder.Services.ConfigureHttpJsonOptions(json =>
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton(_ => CreatePolicy(options, DateTime.UtcNow.Year));
        builder.Services.AddSingleton<IHoursPolicyProvider, HoursEmployeePolicyProvider>();
        builder.Services.AddSingleton(provider => new HoursService(
            provider.GetRequiredService<IHoursJournal>(),
            provider.GetRequiredService<IHoursPolicyProvider>(),
            provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<HoursQueryService>();
        builder.Services.AddHealthChecks()
            .AddCheck(
                "hours-ready",
                () => HealthCheckResult.Healthy("The Hours host has started its storage and security services."));
        builder.Services.AddRateLimiter(HoursRateLimitPolicies.AddTo);
        builder.Services.AddSignalR();
        var secureCookies = !builder.Environment.IsDevelopment();
        var cookiePrefix = secureCookies ? "__Host-NovolisHours" : "NovolisHours";
        builder.Services.AddAntiforgery(antiforgery =>
        {
            antiforgery.HeaderName = "X-Novolis-Hours-CSRF";
            antiforgery.Cookie.Name = $"{cookiePrefix}.Antiforgery";
            antiforgery.Cookie.HttpOnly = false;
            antiforgery.Cookie.SecurePolicy = secureCookies
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            antiforgery.Cookie.Path = "/";
            antiforgery.Cookie.SameSite = SameSiteMode.Strict;
        });
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = $"{cookiePrefix}.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = secureCookies
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.Cookie.Path = "/";
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ValidateSessionAsync,
                    OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    },
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddWorkflow("hours.approval-deadline", workflow => workflow
            .Accepts<HoursApprovalDeadlineCheck>()
            .Then<HoursApprovalDeadlineWorkflowStep, HoursApprovalDeadlineCheck, HoursApprovalPeriod>()
            .EndWith<HoursApprovalDeadlineWorkflowSink, HoursApprovalPeriod>());
        builder.Services.AddHostedService<HoursBootstrapService>();
        builder.Services.AddHostedService<HoursRealtimeProjectionService>();
        builder.Services.AddHostedService<HoursApprovalDeadlineScheduler>();
        builder.Services.AddHostedService<HoursFlexSettlementScheduler>();

        var app = builder.Build();
        app.UseExceptionHandler(errorApplication =>
            errorApplication.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var (statusCode, title) = ClassifyException(exception);
                context.Response.Headers.CacheControl = "no-store";
                await Results.Problem(
                    statusCode: statusCode,
                    title: title,
                    detail: statusCode >= StatusCodes.Status500InternalServerError
                        ? "The request could not be completed."
                        : "The request was not accepted.")
                    .ExecuteAsync(context);
            }));

        var forwardedHeaders = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };
        forwardedHeaders.KnownProxies.Clear();
        forwardedHeaders.KnownIPNetworks.Clear();
        foreach (var addressText in options.TrustedProxyAddresses)
        {
            if (IPAddress.TryParse(addressText, out var address))
            {
                forwardedHeaders.KnownProxies.Add(address);
            }
            else
            {
                app.Logger.LogWarning(
                    "Ignoring invalid Hours trusted proxy address {TrustedProxyAddress}.",
                    addressText);
            }
        }

        app.UseForwardedHeaders(forwardedHeaders);
        app.Use(async (context, next) =>
        {
            if (options.RequireHttps &&
                !app.Environment.IsDevelopment() &&
                !context.Request.IsHttps &&
                !HoursSecurityBoundary.IsHealthProbe(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                context.Response.Headers.Location =
                    $"{Uri.UriSchemeHttps}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
                await Results.Problem(
                        statusCode: StatusCodes.Status426UpgradeRequired,
                        title: "HTTPS is required.",
                        detail: "Use the HTTPS endpoint for the Hours service.")
                    .ExecuteAsync(context);
                return;
            }

            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; connect-src 'self' ws: wss:; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'wasm-unsafe-eval'";
            await next(context);
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api") &&
                !HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) &&
                !HttpMethods.IsOptions(context.Request.Method))
            {
                try
                {
                    await context.RequestServices
                        .GetRequiredService<IAntiforgery>()
                        .ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException exception)
                {
                    app.Logger.LogWarning(
                        exception,
                        "Hours antiforgery validation failed for {Method} {Path}.",
                        context.Request.Method,
                        context.Request.Path);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await Results.Problem(
                            statusCode: StatusCodes.Status400BadRequest,
                            title: "The antiforgery token is missing or invalid.",
                            detail: "Obtain a fresh token and submit the request again.")
                        .ExecuteAsync(context);
                    return;
                }
            }

            await next(context);
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready").AllowAnonymous();
        app.MapGet("/health", () => Results.NotFound()).AllowAnonymous();
        app.MapHub<HoursHub>("/hubs/hours");
        HoursEndpointMappings.Map(app);
        app.MapFallbackToFile("index.html");

        if (options.EnableDemoAdminCredentials)
        {
            app.Logger.LogWarning(
                "Novolis Hours demo credentials are enabled. They are intended only for a local demonstration and must be disabled before deployment.");
        }

        var legalPreset = app.Services.GetRequiredService<HoursPolicy>().LegalPreset;
        if (legalPreset.ReviewState != LegalReviewState.Approved)
        {
            app.Logger.LogWarning(
                "Hours legal preset {LegalPresetId} is {LegalReviewState}; it remains an informational starter until tenant legal review approves it.",
                legalPreset.Id,
                legalPreset.ReviewState);
        }

        return app;
    }

    private static void ConfigureStorage(IServiceCollection services, HoursServerOptions options)
    {
        if (options.UseInMemoryJournal)
        {
            services.AddStorage(storage => storage.AddInMemoryProvider());
            services.AddInMemoryHoursJournal();
            return;
        }

        services.AddJsonHoursJournal(options.DataPath);
    }

    private static HoursPolicy CreatePolicy(HoursServerOptions options, int year)
    {
        var policy = HoursPolicyCatalog.Create(options.LegalPresetId, year);
        return string.IsNullOrWhiteSpace(options.OvertimeAgreementMessage)
            ? policy
            : policy with
            {
                LegalPreset = policy.LegalPreset.WithOvertimeAgreementMessage(options.OvertimeAgreementMessage),
            };
    }

    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var options = context.HttpContext.RequestServices.GetRequiredService<HoursServerOptions>();
        var isDemo = string.Equals(
            principal?.FindFirstValue(HoursClaimTypes.Demo),
            bool.TrueString,
            StringComparison.Ordinal);
        if (isDemo)
        {
            if (!options.EnableDemoAdminCredentials ||
                !HoursSecurityBoundary.IsLoopback(context.HttpContext))
            {
                context.RejectPrincipal();
            }

            return;
        }

        var sessionId = principal?.FindFirstValue(HoursClaimTypes.SessionId);
        var identityText = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sessionId) ||
            !IdentityId.TryParse(identityText, out var claimedIdentity))
        {
            context.RejectPrincipal();
            return;
        }

        var authentication = context.HttpContext.RequestServices.GetRequiredService<AuthenticationFacade>();
        var authenticatedIdentity = await authentication.GetAuthenticatedIdentityAsync(
            sessionId,
            context.HttpContext.RequestAborted);
        if (authenticatedIdentity is null || authenticatedIdentity.Value != claimedIdentity)
        {
            context.RejectPrincipal();
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<HoursUserDirectory>();
        var user = await users.FindAsync(claimedIdentity.Value, context.HttpContext.RequestAborted);
        if (user is null)
        {
            context.RejectPrincipal();
            return;
        }

        context.ReplacePrincipal(HoursPrincipalFactory.Create(user, claimedIdentity, sessionId));
        context.ShouldRenew = true;
    }

    private static (int StatusCode, string Title) ClassifyException(Exception? exception) =>
        exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "The authenticated principal is not allowed."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "The requested Hours record was not found."),
            ArgumentException => (StatusCodes.Status400BadRequest, "The request contains invalid values."),
            InvalidOperationException => (StatusCodes.Status409Conflict, "The Hours operation is not valid in its current state."),
            _ => (StatusCodes.Status500InternalServerError, "The Hours service encountered an error."),
        };
}
