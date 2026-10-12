using Android.Content;
using AndroidX.Core.Content;
using PresenceLedger.App;

namespace PresenceLedger.Android;

/// <summary>Writes the presence report into app storage and opens the share sheet.</summary>
public sealed class AndroidReportExport : IReportExport
{
    /// <summary>Creates an exporter that stages reports under app-owned files.</summary>
    public AndroidReportExport()
    {
    }

    /// <inheritdoc />
    public Task ExportAsync(string report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        var path = AndroidLedgerShareStaging.StageText(
            context,
            "presence-report.txt",
            report);
        var authority = $"{context.PackageName}.ledger";
        var uri = FileProvider.GetUriForFile(context, authority, new Java.IO.File(path))
            ?? throw new InvalidOperationException("Unable to share the presence report.");

        using var intent = new Intent(Intent.ActionSend)
            .SetType("text/plain")
            .PutExtra(Intent.ExtraStream, uri)
            .PutExtra(Intent.ExtraSubject, "Presence Ledger")
            .PutExtra(Intent.ExtraText, report)
            .AddFlags(ActivityFlags.GrantReadUriPermission);
        intent.ClipData = ClipData.NewRawUri("Presence Ledger", uri);

        using var chooser = (Intent.CreateChooser(intent, "Export report")
            ?? throw new InvalidOperationException("Android could not create a report share chooser."))
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
        context.StartActivity(chooser);
        return Task.CompletedTask;
    }
}
