using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using PresenceLedger.App;
using PresenceLedger.Storage;

namespace PresenceLedger.Android;

/// <summary>Shares every ledger file through the Android chooser.</summary>
public sealed class AndroidLedgerShare : ILedgerFileShare
{
    readonly string _sourceRoot;

    /// <summary>Creates a share action for the private ledger root.</summary>
    public AndroidLedgerShare(string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        _sourceRoot = sourceRoot;
    }

    /// <inheritdoc />
    public Task ShareAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = LedgerFiles.List(_sourceRoot);
        if (files.Count == 0)
            throw new InvalidOperationException("No ledger files have been stored yet.");

        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        var authority = $"{context.PackageName}.ledger";
        var uris = new List<IParcelable>(files.Count);
        foreach (var file in files)
        {
            var uri = FileProvider.GetUriForFile(
                context,
                authority,
                new Java.IO.File(file.FullPath))
                ?? throw new InvalidOperationException($"Unable to share {file.RelativePath}.");
            uris.Add(uri);
        }

        using var intent = new Intent(Intent.ActionSendMultiple)
            .SetType("application/x-ndjson")
            .AddFlags(ActivityFlags.GrantReadUriPermission);
        intent.PutParcelableArrayListExtra(Intent.ExtraStream, uris);
        intent.ClipData = ClipDataFor(uris);

        using var chooser = (Intent.CreateChooser(intent, "Share ledger")
            ?? throw new InvalidOperationException("Android could not create a ledger share chooser."))
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
        context.StartActivity(chooser);
        return Task.CompletedTask;
    }

    static ClipData ClipDataFor(IReadOnlyList<IParcelable> uris)
    {
        var first = (global::Android.Net.Uri)uris[0];
        var clip = new ClipData(
            "Presence Ledger",
            ["application/x-ndjson"],
            new ClipData.Item(first));
        for (var index = 1; index < uris.Count; index++)
            clip.AddItem(new ClipData.Item((global::Android.Net.Uri)uris[index]));

        return clip;
    }
}
