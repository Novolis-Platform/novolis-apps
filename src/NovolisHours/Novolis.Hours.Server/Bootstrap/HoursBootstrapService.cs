using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Hours.Application;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;
using Novolis.Security.Authentication;
using AuthenticationFacade = Novolis.Security.Authentication.IAuthenticationService;

namespace Novolis.Hours.Server;

/// <summary>Requires an explicit non-demo administrator bootstrap while keeping the local demo path isolated.</summary>
public sealed class HoursBootstrapService : IHostedService
{
    private readonly HoursServerOptions options;
    private readonly HoursUserDirectory users;
    private readonly AuthenticationFacade authentication;
    private readonly ILogger<HoursBootstrapService> logger;

    /// <summary>Initializes the secure administrator bootstrapper.</summary>
    public HoursBootstrapService(
        HoursServerOptions options,
        HoursUserDirectory users,
        AuthenticationFacade authentication,
        ILogger<HoursBootstrapService> logger)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.users = users ?? throw new ArgumentNullException(nameof(users));
        this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.EnableDemoAdminCredentials || users.List().Any(user => user.Role == HoursActorRole.Administrator))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.InitialAdministratorPassword))
        {
            throw new InvalidOperationException(
                "Novolis Hours requires an existing administrator or Hours__InitialAdministratorPassword when demo credentials are disabled.");
        }

        var result = await authentication.RegisterAsync(
            options.InitialAdministratorLogin,
            options.InitialAdministratorPassword,
            options.InitialAdministratorDisplayName,
            createSession: false,
            cancellationToken);
        if (!result.Succeeded || result.IdentityId is not { } identity)
        {
            throw new InvalidOperationException(
                $"The initial Novolis Hours administrator could not be provisioned: {result.Error ?? "unknown security error"}.");
        }

        await users.SaveAsync(
            new HoursUserDocument(
                identity.Value,
                options.InitialAdministratorEmployeeId,
                options.InitialAdministratorLogin,
                options.InitialAdministratorDisplayName,
                HoursActorRole.Administrator),
            cancellationToken);
        logger.LogInformation(
            "Provisioned the initial Novolis Hours administrator {EmployeeId} through Novolis Security.",
            options.InitialAdministratorEmployeeId);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
