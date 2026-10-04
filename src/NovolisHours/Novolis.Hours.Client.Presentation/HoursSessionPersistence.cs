using System.Text.Json;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Persists the Hours service URL and last-used login on the local machine.</summary>
public static class HoursSessionPersistence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Default file under the Novolis Hours app-data root.</summary>
    public static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Hours",
            "client-session.json");

    /// <summary>Loads a previously stored session, or a default pointing at the local service.</summary>
    public static HoursSessionModel Load()
    {
        var model = new HoursSessionModel
        {
            ServiceUrl = Environment.GetEnvironmentVariable("NOVOLIS_HOURS_SERVICE_URL")
                ?? "https://localhost:5700/",
        };
        if (!File.Exists(FilePath))
        {
            return model;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<HoursSessionModel>(File.ReadAllText(FilePath), Json);
            if (stored is not null && !string.IsNullOrWhiteSpace(stored.ServiceUrl))
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NOVOLIS_HOURS_SERVICE_URL")))
                {
                    model.ServiceUrl = stored.ServiceUrl;
                }

                model.EmployeeId = stored.EmployeeId;
                model.DisplayName = stored.DisplayName;
                model.OrganisationId = stored.OrganisationId;
                model.TimeZoneId = stored.TimeZoneId;
            }
        }
        catch (JsonException)
        {
            // Keep the environment default when the file is unreadable.
        }

        return model;
    }

    /// <summary>Writes the current session facts for the next launch.</summary>
    public static void Save(HoursSessionModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(session, Json));
    }
}
