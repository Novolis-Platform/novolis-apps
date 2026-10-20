using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Novolis.Transports.Torrent;

namespace Novolis.Torrent;

public sealed class TorrentTransferItem : INotifyPropertyChanged
{
    private TorrentProgressInfo? _progress;
    private string _status = "Ready";
    private bool _isRunning;

    public TorrentTransferItem(
        TorrentInfo torrent,
        string torrentPath,
        string downloadDirectory)
    {
        Torrent = torrent ?? throw new ArgumentNullException(nameof(torrent));
        TorrentPath = torrentPath ?? throw new ArgumentNullException(nameof(torrentPath));
        DownloadDirectory = downloadDirectory
            ?? throw new ArgumentNullException(nameof(downloadDirectory));
        Name = torrent.Files.FirstOrDefault()?.FilePath
            ?? Path.GetFileNameWithoutExtension(torrentPath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TorrentInfo Torrent { get; }

    public string Name { get; }

    public string TorrentPath { get; }

    public string DownloadDirectory { get; }

    public TorrentProgressInfo? Progress => _progress;

    public string Status => _status;

    public bool IsRunning => _isRunning;

    public string SizeText => FormatBytes(Torrent.Length);

    public string ProgressText =>
        $"{(_progress?.CompletedPercentage ?? 0m).ToString("0.0", CultureInfo.InvariantCulture)}%";

    public double ProgressFraction =>
        (double)Math.Clamp(
            (_progress?.CompletedPercentage ?? 0m) / 100m,
            0m,
            1m);

    public string MetricsText
    {
        get
        {
            if (_progress is null)
                return $"{Torrent.PiecesCount:N0} pieces · waiting to start";

            return string.Join(
                "  ·  ",
                [
                    $"{_progress.SeederCount:N0} seeds",
                    $"{_progress.LeecherCount:N0} peers",
                    $"↓ {FormatRate(_progress.DownloadSpeed)}",
                    $"↑ {FormatRate(_progress.UploadSpeed)}",
                ]);
        }
    }

    internal void SetRunning(bool running, string status)
    {
        _isRunning = running;
        _status = status;
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(Status));
    }

    internal void SetProgress(TorrentProgressInfo? progress, string status)
    {
        _progress = progress;
        _status = status;
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(MetricsText));
        OnPropertyChanged(nameof(Status));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes:N0} B";

        var value = (double)bytes;
        var units = new[] { "KB", "MB", "GB", "TB" };
        var index = -1;
        do
        {
            value /= 1024;
            index++;
        }
        while (value >= 1024 && index < units.Length - 1);

        return $"{value:0.##} {units[index]}";
    }

    private static string FormatRate(decimal bytesPerSecond) =>
        $"{FormatBytes((long)Math.Max(0, bytesPerSecond))}/s";
}
