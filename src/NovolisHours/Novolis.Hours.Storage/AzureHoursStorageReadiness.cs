using Azure.Data.Tables;

namespace Novolis.Hours.Storage;

/// <summary>Readiness probe for Azure Table Storage and Azurite.</summary>
public sealed class AzureHoursStorageReadiness(TableServiceClient service) : IHoursStorageReadiness
{
    private readonly TableServiceClient service =
        service ?? throw new ArgumentNullException(nameof(service));

    /// <inheritdoc />
    public async ValueTask<HoursStorageProbeResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await service.GetPropertiesAsync(cancellationToken).ConfigureAwait(false);
            return new HoursStorageProbeResult(
                true,
                "The Azure Table service is reachable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new HoursStorageProbeResult(
                false,
                $"The Azure Table service is not reachable ({exception.GetType().Name}).");
        }
    }
}
