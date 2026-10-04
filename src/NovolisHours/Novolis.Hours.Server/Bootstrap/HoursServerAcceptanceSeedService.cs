using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Hours.Application;
using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Storage;
using Novolis.Security.Authentication;
using AuthenticationFacade = Novolis.Security.Authentication.IAuthenticationService;

namespace Novolis.Hours.Server;

/// <summary>Seeds the named, real-authenticated identities used by the Hours acceptance regime.</summary>
public sealed class HoursServerAcceptanceSeedService : IHostedService
{
    private readonly HoursServerOptions options;
    private readonly HoursUserDirectory users;
    private readonly AuthenticationFacade authentication;
    private readonly IHoursJournal journal;
    private readonly HoursAcceptanceConfiguration configuration;
    private readonly ILogger<HoursServerAcceptanceSeedService> logger;

    /// <summary>Initializes the development acceptance seeder.</summary>
    public HoursServerAcceptanceSeedService(
        HoursServerOptions options,
        HoursUserDirectory users,
        AuthenticationFacade authentication,
        IHoursJournal journal,
        HoursAcceptanceConfiguration configuration,
        ILogger<HoursServerAcceptanceSeedService> logger)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.users = users ?? throw new ArgumentNullException(nameof(users));
        this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.EnableAcceptanceSeed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AcceptanceSeedPassword))
        {
            throw new InvalidOperationException(
                "Hours acceptance identities require Hours__AcceptanceSeedPassword.");
        }

        foreach (var definition in Definitions)
        {
            var user = users.FindByEmployeeId(definition.EmployeeId);
            if (user is null)
            {
                var result = await authentication.RegisterAsync(
                    definition.Login,
                    options.AcceptanceSeedPassword,
                    definition.DisplayName,
                    createSession: false,
                    cancellationToken);
                if (!result.Succeeded || result.IdentityId is not { } identity)
                {
                    throw new InvalidOperationException(
                        $"The acceptance identity '{definition.Login}' could not be provisioned: {result.Error ?? "unknown security error"}.");
                }

                user = new HoursUserDocument(
                    identity.Value,
                    definition.EmployeeId,
                    definition.Login,
                    definition.DisplayName,
                    definition.Role,
                    definition.LegalPresetId,
                    OrganisationId: definition.OrganisationId,
                    DivisionId: definition.DivisionId,
                    TeamId: definition.TeamId,
                    ApprovalLevel: definition.ApprovalLevel);
                await users.SaveAsync(user, cancellationToken);
                logger.LogInformation(
                    "Provisioned Hours acceptance identity {EmployeeId} with role {Role}.",
                    definition.EmployeeId,
                    definition.Role);
            }
            else if (user.Role != definition.Role ||
                     !string.Equals(user.LegalPresetId, definition.LegalPresetId, StringComparison.Ordinal) ||
                     !string.Equals(user.OrganisationId, definition.OrganisationId, StringComparison.Ordinal) ||
                     !string.Equals(user.DivisionId, definition.DivisionId, StringComparison.Ordinal) ||
                     !string.Equals(user.TeamId, definition.TeamId, StringComparison.Ordinal) ||
                     user.ApprovalLevel != definition.ApprovalLevel)
            {
                user = user with
                {
                    Role = definition.Role,
                    LegalPresetId = definition.LegalPresetId,
                    OrganisationId = definition.OrganisationId,
                    DivisionId = definition.DivisionId,
                    TeamId = definition.TeamId,
                    ApprovalLevel = definition.ApprovalLevel,
                };
                await users.SaveAsync(user, cancellationToken);
                logger.LogInformation(
                    "Reconciled Hours acceptance profile {EmployeeId} with role {Role}.",
                    definition.EmployeeId,
                    definition.Role);
            }

            var initialSnapshot = configuration.GetSnapshot(
                definition.EmployeeId,
                new DateOnly(2026, 1, 1));
            var existingEvents = await journal.ReadEmployeeAsync(
                definition.EmployeeId,
                cancellationToken);
            if (!existingEvents
                .Where(entry => entry.Type == HoursEventType.ConfigurationSnapshotRecorded)
                .Select(entry => entry.ReadPayload<ConfigurationSnapshot>())
                .Any(snapshot => snapshot.Id == initialSnapshot.Id))
            {
                await journal.AppendAsync(
                    HoursEvent.Create(
                        definition.EmployeeId,
                        HoursEventType.ConfigurationSnapshotRecorded,
                        initialSnapshot,
                        new HoursActor(
                            definition.EmployeeId,
                            definition.DisplayName,
                            definition.Role),
                        DateTimeOffset.UtcNow),
                    cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IReadOnlyList<AcceptanceIdentity> Definitions { get; } =
    [
        new("ada", "ada", "Ada Lovelace", HoursActorRole.Employee, "norway.private.flex", HoursCustomerCatalog.NordvikOffice, "division-north", "team-a"),
        new("bob", "bob", "Bob Stone", HoursActorRole.Employee, "norway.state.flex", HoursCustomerCatalog.NordvikStation, "division-north", "team-b"),
        new("alice", "alice", "Alice Manager", HoursActorRole.Manager, "norway.private.flex", HoursCustomerCatalog.NordvikOffice, "division-north", "team-a", 1),
        new("nina", "nina", "Nina Manager", HoursActorRole.Manager, "norway.state.flex", HoursCustomerCatalog.NordvikStation, "division-north", "team-b", 1),
        new("charlie", "charlie", "Charlie Director", HoursActorRole.Manager, "norway.private.flex", HoursCustomerCatalog.NordvikOffice, "division-north", null, 2),
        new("helen", "helen", "Helen HR", HoursActorRole.HumanResources, "norway.private.flex", HoursCustomerCatalog.NordvikOffice, "division-north", null),
        new("audrey", "audrey", "Audrey Auditor", HoursActorRole.Auditor, "norway.private.flex", HoursCustomerCatalog.NordvikOffice, "division-north", null),
        new("system", "system", "Platform System", HoursActorRole.System, "hours.platform", HoursCustomerCatalog.Platform, null, null),
        new("pierre", "pierre", "Pierre Curie", HoursActorRole.Employee, "france.annualisation", HoursCustomerCatalog.AtelierCurie, "division-france", "team-france"),
        new("marc", "marc", "Marc Approver", HoursActorRole.Manager, "france.annualisation", HoursCustomerCatalog.AtelierCurie, "division-france", "team-france", 1),
        new("anna", "anna", "Anna Kowalska", HoursActorRole.Employee, "poland.okres-rozliczeniowy", HoursCustomerCatalog.WarsawSettlement, "division-poland", "team-poland"),
        new("kasia", "kasia", "Kasia Employer", HoursActorRole.Manager, "poland.okres-rozliczeniowy", HoursCustomerCatalog.WarsawSettlement, "division-poland", "team-poland", 1),
        new("liisa", "liisa", "Liisa Virtanen", HoursActorRole.Employee, "finland.liukuva-tyoaika", HoursCustomerCatalog.HelsinkiFlex, "division-finland", "team-finland"),
        new("jamie", "jamie", "Jamie Shaw", HoursActorRole.Employee, "england.retail.rigid", HoursCustomerCatalog.GameRetail, "division-uk", "team-high-street"),
        new("priya", "priya", "Priya Shah", HoursActorRole.HumanResources, "england.retail.rigid", HoursCustomerCatalog.GameRetail, "division-uk", null),
    ];

    private sealed record AcceptanceIdentity(
        string EmployeeId,
        string Login,
        string DisplayName,
        HoursActorRole Role,
        string LegalPresetId,
        string OrganisationId,
        string? DivisionId,
        string? TeamId,
        int? ApprovalLevel = null);
}
