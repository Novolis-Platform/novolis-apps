using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Novolis.Hours.Application;
using Novolis.Hours.Contracts;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Security.Authentication;
using AuthenticationFacade = Novolis.Security.Authentication.IAuthenticationService;

namespace Novolis.Hours.Server;

/// <summary>Maps the authenticated JSON API and report endpoints for the self-contained Hours host.</summary>
public static class HoursServerEndpointMappings
{
    /// <summary>Adds endpoint routes to the host.</summary>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/antiforgery", (IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new AntiforgeryResponse(tokens.RequestToken ?? string.Empty));
        }).AllowAnonymous();

        endpoints.MapPost("/api/auth/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(HoursServerRateLimitPolicies.Login);

        var api = endpoints.MapGroup("/api").RequireAuthorization();
        api.MapGet("/auth/me", (HttpContext context) =>
        {
            var actor = HoursServerPrincipalFactory.ToActor(context.User);
            return Results.Ok(new CurrentUserResponse(actor.Id, actor.DisplayName, actor.Role));
        });
        api.MapPost("/auth/logout", LogoutAsync);
        api.MapPost("/auth/password", ChangePasswordAsync);

        api.MapGet("/configuration/employees/{employeeId}", GetEmployeeConfiguration);
        api.MapGet("/employees/{employeeId}/view", GetEmployeeViewAsync);
        api.MapGet("/query/{kind}", ExecuteQueryAsync);
        api.MapPost("/work", RegisterWorkAsync);
        api.MapPost("/adjustments", ProposeAdjustmentAsync);
        api.MapPost("/flex-normalizations", ProposeFlexNormalizationAsync);
        api.MapPost("/adjustments/{adjustmentId:guid}/response", RespondToAdjustmentAsync);
        api.MapPost("/adjustments/{adjustmentId:guid}/resolve", ResolveAdjustmentAsync);
        api.MapPost("/approval-periods", OpenApprovalPeriodAsync);
        api.MapPost("/approval-periods/{periodId:guid}/submit", SubmitPeriodAsync);
        api.MapPost("/approval-periods/{periodId:guid}/review", ReviewPeriodAsync);
        api.MapPost("/approval-periods/{periodId:guid}/escalate", EscalatePeriodAsync);
        api.MapGet("/reports/{employeeId}.html", ExportHtmlReportAsync);
        api.MapGet("/legal/presets", (HoursPolicy policy) => Results.Ok(new LegalPresetResponse(
            policy.LegalPreset.Id,
            policy.LegalPreset.Version,
            policy.LegalPreset.CountryCode,
            policy.LegalPreset.Citation,
            policy.LegalPreset.ReviewState,
            policy.LegalPreset.OvertimeAgreementMessage,
            policy.LegalPreset.ApprovalSchedule.EmployeeSubmitBusinessDays,
            policy.LegalPreset.ApprovalSchedule.ManagerReviewBusinessDays,
            policy.LegalPreset.ApprovalSchedule.HrResolutionBusinessDays)));
        api.MapGet("/legal/preset-catalog", GetLegalPresetCatalog);
        api.MapGet("/admin/users", ListUsers);
        api.MapPost("/admin/users", CreateUserAsync);
        api.MapPut("/admin/users/{employeeId}/worktime-settings", UpdateWorktimeSettingsAsync);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        HoursServerOptions options,
        AuthenticationFacade authentication,
        HoursUserDirectory users)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["login"] = ["Login and password are required."],
            });
        }

        if (options.EnableDemoAdminCredentials &&
            HoursServerSecurityBoundary.IsLoopback(context) &&
            string.Equals(request.Login, HoursServerDemoAccount.Login, StringComparison.Ordinal) &&
            string.Equals(request.Password, HoursServerDemoAccount.Password, StringComparison.Ordinal))
        {
            var principal = HoursServerPrincipalFactory.CreateDemo();
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { AllowRefresh = true });
            return Results.Ok(ToLoginResponse(HoursServerDemoAccount.User, isDemoAdministrator: true));
        }

        var signIn = await authentication.SignInAsync(
            request.Login,
            request.Password,
            createSession: true,
            cancellationToken: context.RequestAborted);
        if (!signIn.Succeeded || signIn.IdentityId is not { } identity || string.IsNullOrWhiteSpace(signIn.SessionId))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindAsync(identity.Value, context.RequestAborted);
        if (user is null)
        {
            await authentication.SignOutAsync(signIn.SessionId, context.RequestAborted);
            return Results.Problem(
                "The authenticated account has no Novolis Hours role profile.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var authenticatedPrincipal = HoursServerPrincipalFactory.Create(user, identity, signIn.SessionId);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authenticatedPrincipal,
            new AuthenticationProperties { AllowRefresh = true });
        return Results.Ok(ToLoginResponse(user));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext context,
        AuthenticationFacade authentication,
        HoursUserDirectory users)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["The current and new passwords are required."],
            });
        }

        var identityText = context.User.FindFirst(HoursServerClaimTypes.IdentityId)?.Value;
        if (!Guid.TryParse(identityText, out var identityId))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindAsync(identityId, context.RequestAborted);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await authentication.ChangePasswordAsync(
            user.Login,
            request.CurrentPassword,
            request.NewPassword,
            context.RequestAborted);
        if (!result.Succeeded)
        {
            return result.Error is "password_too_short" or "password_forbidden" or "password_breached"
                ? Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["The new password does not meet the configured security policy."],
                })
                : Results.Unauthorized();
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        AuthenticationFacade authentication)
    {
        var sessionId = context.User.FindFirst(HoursServerClaimTypes.SessionId)?.Value;
        if (!string.IsNullOrWhiteSpace(sessionId) && sessionId != "demo")
        {
            await authentication.SignOutAsync(sessionId, context.RequestAborted);
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetEmployeeViewAsync(
        string employeeId,
        HttpContext context,
        HoursService hours)
    {
        var forbidden = EnsureCanView(context.User, employeeId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        return Results.Ok(await hours.GetEmployeeViewAsync(employeeId, context.RequestAborted));
    }

    private static IResult GetEmployeeConfiguration(
        string employeeId,
        HttpContext context,
        IHoursPolicyProvider policies)
    {
        var forbidden = EnsureCanView(context.User, employeeId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var policy = policies.GetPolicy(employeeId);
        var profile = policy.EmploymentSettings.Profile;
        var settings = policy.EmploymentSettings;
        return Results.Ok(new EmployeeConfigurationResponse(
            employeeId,
            new ProfileResponse(
                profile.Id,
                profile.Name,
                profile.WorkingDayEnvelope.Start,
                profile.WorkingDayEnvelope.End,
                profile.CoreHours.Start,
                profile.CoreHours.End,
                profile.Lunch.Start,
                profile.Lunch.End,
                profile.WeekHours,
                profile.DayHours,
                profile.UnmarkedSurplusClassification),
            new TemplateResponse(settings.Template.Id, settings.Template.Name),
            new IndividualSettingsResponse(
                settings.WorkFraction,
                settings.ExpectedIntervalOverride?.Start,
                settings.ExpectedIntervalOverride?.End),
            new CalendarResponse(
                settings.Calendar.Id,
                settings.Calendar.Source.Source,
                settings.Calendar.Source.Version,
                settings.Calendar.Source.CountryCode),
            new LegalPresetResponse(
                policy.LegalPreset.Id,
                policy.LegalPreset.Version,
                policy.LegalPreset.CountryCode,
                policy.LegalPreset.Citation,
                policy.LegalPreset.ReviewState,
                policy.LegalPreset.OvertimeAgreementMessage,
                policy.LegalPreset.ApprovalSchedule.EmployeeSubmitBusinessDays,
                policy.LegalPreset.ApprovalSchedule.ManagerReviewBusinessDays,
                policy.LegalPreset.ApprovalSchedule.HrResolutionBusinessDays),
            policy.SettlementPolicy.Id,
            policy.SettlementPolicy.Cadence));
    }

    private static async Task<IResult> ExecuteQueryAsync(
        string kind,
        string employeeId,
        DateOnly? from,
        DateOnly? through,
        HttpContext context,
        HoursQueryService queries)
    {
        var forbidden = EnsureCanView(context.User, employeeId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (!Enum.TryParse<HoursQueryKind>(kind, ignoreCase: true, out var queryKind))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["kind"] = [$"Unknown query kind '{kind}'."],
            });
        }

        return Results.Ok(await queries.ExecuteAsync(
            new HoursQuery(employeeId, queryKind, from, through),
            context.RequestAborted));
    }

    private static IResult GetLegalPresetCatalog()
    {
        var year = DateTime.UtcNow.Year;
        var catalog = HoursPolicyCatalog.PresetIds
            .Select(id => HoursPolicyCatalog.Create(id, year).LegalPreset)
            .Select(preset => new LegalPresetResponse(
                preset.Id,
                preset.Version,
                preset.CountryCode,
                preset.Citation,
                preset.ReviewState,
                preset.OvertimeAgreementMessage,
                preset.ApprovalSchedule.EmployeeSubmitBusinessDays,
                preset.ApprovalSchedule.ManagerReviewBusinessDays,
                preset.ApprovalSchedule.HrResolutionBusinessDays))
            .ToArray();
        return Results.Ok(catalog);
    }

    private static async Task<IResult> RegisterWorkAsync(
        RegisterWorkRequest request,
        HttpContext context,
        HoursService hours)
    {
        var actor = HoursServerPrincipalFactory.ToActor(context.User);
        var result = await hours.RegisterWorkAsync(
            new RegisterWorkCommand(
                request.EmployeeId,
                request.Day,
                request.StartedAt,
                request.EndedAt,
                request.BreakStartedAt,
                request.BreakEndedAt,
                request.FinancialCompensationSlices ?? [],
                request.Comment,
                request.ManagerAgreementRecorded,
                actor),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Created($"/api/employees/{result.EmployeeId}/view", result);
    }

    private static async Task<IResult> ProposeAdjustmentAsync(
        ProposeAdjustmentRequest request,
        HttpContext context,
        HoursService hours)
    {
        var adjustment = await hours.ProposeAdjustmentAsync(
            new ProposeAdjustmentCommand(
                request.EmployeeId,
                request.EffectiveDay,
                request.DurationDelta,
                request.Reason,
                request.Comment,
                HoursServerPrincipalFactory.ToActor(context.User)),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Created($"/api/adjustments/{adjustment.Id}", adjustment);
    }

    private static async Task<IResult> RespondToAdjustmentAsync(
        Guid adjustmentId,
        RespondToAdjustmentRequest request,
        HttpContext context,
        HoursService hours)
    {
        var adjustment = await hours.RespondToAdjustmentAsync(
            new RespondToAdjustmentCommand(
                adjustmentId,
                request.Response,
                request.Comment,
                HoursServerPrincipalFactory.ToActor(context.User)),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Ok(adjustment);
    }

    private static async Task<IResult> ProposeFlexNormalizationAsync(
        ProposeFlexNormalizationRequest request,
        HttpContext context,
        HoursService hours)
    {
        var adjustment = await hours.ProposeFlexNormalizationAsync(
            request.EmployeeId,
            request.AssessedThrough,
            HoursServerPrincipalFactory.ToActor(context.User),
            context.RequestAborted);
        return adjustment is null
            ? Results.NoContent()
            : Results.Created($"/api/adjustments/{adjustment.Id}", adjustment);
    }

    private static async Task<IResult> ResolveAdjustmentAsync(
        Guid adjustmentId,
        ResolveAdjustmentRequest request,
        HttpContext context,
        HoursService hours)
    {
        var adjustment = await hours.ResolveAdjustmentAsync(
            new ResolveAdjustmentCommand(
                adjustmentId,
                request.Resolution,
                request.Comment,
                HoursServerPrincipalFactory.ToActor(context.User)),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Ok(adjustment);
    }

    private static async Task<IResult> OpenApprovalPeriodAsync(
        OpenApprovalPeriodRequest request,
        HttpContext context,
        HoursService hours)
    {
        var period = await hours.OpenApprovalPeriodAsync(
            new OpenApprovalPeriodCommand(
                request.EmployeeId,
                request.StartsOn,
                request.EndsOn,
                HoursServerPrincipalFactory.ToActor(context.User)),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Created($"/api/approval-periods/{period.Id}", period);
    }

    private static async Task<IResult> SubmitPeriodAsync(
        Guid periodId,
        ApprovalActionRequest request,
        HttpContext context,
        HoursService hours)
    {
        var period = await hours.SubmitApprovalPeriodAsync(
            periodId,
            request.Comment,
            HoursServerPrincipalFactory.ToActor(context.User),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Ok(period);
    }

    private static async Task<IResult> ReviewPeriodAsync(
        Guid periodId,
        ApprovalActionRequest request,
        HttpContext context,
        HoursService hours)
    {
        var period = await hours.ReviewApprovalPeriodAsync(
            periodId,
            request.Comment,
            HoursServerPrincipalFactory.ToActor(context.User),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Ok(period);
    }

    private static async Task<IResult> EscalatePeriodAsync(
        Guid periodId,
        ApprovalActionRequest request,
        HttpContext context,
        HoursService hours)
    {
        var period = await hours.EscalateApprovalPeriodAsync(
            periodId,
            request.Comment,
            HoursServerPrincipalFactory.ToActor(context.User),
            context.RequestAborted);
        HoursServerTelemetry.JournalAppends.Add(1);
        return Results.Ok(period);
    }

    private static async Task<IResult> ExportHtmlReportAsync(
        string employeeId,
        HttpContext context,
        HoursService hours,
        HoursHtmlReportExporter exporter)
    {
        var forbidden = EnsureCanView(context.User, employeeId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var view = await hours.GetEmployeeViewAsync(employeeId, context.RequestAborted);
        return Results.Content(exporter.Export(view), "text/html; charset=utf-8");
    }

    private static IResult ListUsers(HttpContext context, HoursUserDirectory users)
    {
        var forbidden = EnsureAdministrator(context.User);
        return forbidden ?? Results.Ok(users.List());
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        HttpContext context,
        AuthenticationFacade authentication,
        HoursUserDirectory users)
    {
        var forbidden = EnsureAdministrator(context.User);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (string.IsNullOrWhiteSpace(request.EmployeeId) ||
            string.IsNullOrWhiteSpace(request.Login) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["user"] = ["Employee ID, login, and display name are required."],
            });
        }

        if (users.FindByEmployeeId(request.EmployeeId) is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["employeeId"] = ["The employee identifier already has a Novolis Hours role profile."],
            });
        }

        var result = await authentication.RegisterAsync(
            request.Login,
            request.Password,
            request.DisplayName,
            createSession: false,
            context.RequestAborted);
        if (!result.Succeeded || result.IdentityId is not { } identity)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = [result.Error ?? "The account could not be created."],
            });
        }

        var user = new HoursUserDocument(
            identity.Value,
            request.EmployeeId,
            request.Login,
            request.DisplayName,
            request.Role);
        await users.SaveAsync(user, context.RequestAborted);
        return Results.Created($"/api/admin/users/{user.Id}", user);
    }

    private static async Task<IResult> UpdateWorktimeSettingsAsync(
        string employeeId,
        UpdateWorktimeSettingsRequest request,
        HttpContext context,
        HoursUserDirectory users)
    {
        var forbidden = EnsureAdministrator(context.User);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (request.WorkFraction is <= 0 or > 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["workFraction"] = ["Work fraction must be greater than zero and at most one."],
            });
        }

        if (request.ExpectedIntervalOverrideStart.HasValue != request.ExpectedIntervalOverrideEnd.HasValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["expectedIntervalOverride"] = ["Expected interval overrides require both a start and an end."],
            });
        }

        if (request.ExpectedIntervalOverrideStart is { } start &&
            request.ExpectedIntervalOverrideEnd is { } end &&
            end <= start)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["expectedIntervalOverride"] = ["Expected interval end must be after its start."],
            });
        }

        if (!string.IsNullOrWhiteSpace(request.LegalPresetId) &&
            !HoursPolicyCatalog.PresetIds.Contains(request.LegalPresetId, StringComparer.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["legalPresetId"] = ["The legal preset is not supported by this host."],
            });
        }

        var existing = users.FindByEmployeeId(employeeId);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var updated = existing with
        {
            LegalPresetId = request.LegalPresetId,
            WorkFraction = request.WorkFraction,
            ExpectedIntervalOverrideStart = request.ExpectedIntervalOverrideStart,
            ExpectedIntervalOverrideEnd = request.ExpectedIntervalOverrideEnd,
        };
        await users.SaveAsync(updated, context.RequestAborted);
        return Results.Ok(updated);
    }

    private static IResult? EnsureCanView(System.Security.Claims.ClaimsPrincipal principal, string employeeId)
    {
        var actor = HoursServerPrincipalFactory.ToActor(principal);
        return actor.Role == HoursActorRole.Employee &&
            !string.Equals(actor.Id, employeeId, StringComparison.Ordinal)
            ? Results.Forbid()
            : null;
    }

    private static IResult? EnsureAdministrator(System.Security.Claims.ClaimsPrincipal principal) =>
        HoursServerPrincipalFactory.ToActor(principal).Role == HoursActorRole.Administrator
            ? null
            : Results.Forbid();

    private static LoginResponse ToLoginResponse(HoursUserDocument user, bool isDemoAdministrator = false) =>
        new(user.EmployeeId, user.DisplayName, user.Role, isDemoAdministrator);

    

    

}
