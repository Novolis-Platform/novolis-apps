using System.Diagnostics;
using System.Text;
using CoverageStudio.Models;
using Novolis.Avalonia.Controls;
using Novolis.Tools.Coverage;

namespace CoverageStudio.Services;

/// <summary>Workspace discovery, test runs, Cobertura collection, and CRAP analysis.</summary>
internal sealed class CoverageSession
{
    private readonly object _logGate = new();
    private readonly StringBuilder _log = new();
    private CancellationTokenSource? _runCts;

    public string Root { get; private set; } = TryResolveRoot();
    public string OutputDir { get; set; }
    public bool PlatformMode { get; set; } = true;
    public bool SkipBuild { get; set; } = true;
    public bool RegenerateSlnx { get; set; }
    public double FailBelow { get; set; } = -1;
    public double CrapThreshold { get; set; } = CrapScore.DefaultThreshold;
    public int ThrottleLimit { get; set; }

    public CoverageSession()
    {
        OutputDir = Path.Combine(Root, "coverage");
    }

    public IReadOnlyList<RepoListItem> Repos { get; private set; } = [];
    public CoverageCollectResult? LastCollect { get; private set; }
    public CrapReport? LastCrap { get; private set; }
    public CoberturaDocument? LastDocument { get; private set; }
    public string? HtmlIndexPath { get; private set; }
    public bool IsBusy => _active;

    private bool _active;

    public event Action? Changed;
    public event Action? LogChanged;

    public string LogText
    {
        get
        {
            lock (_logGate)
                return _log.ToString();
        }
    }

    public void SetRoot(string root)
    {
        Root = Path.GetFullPath(root);
        OutputDir = Path.Combine(Root, "coverage");
        RaiseChanged();
    }

    public void Discover()
    {
        EnsureRoot();
        var excludeFile = CoverageWorkspace.DefaultExcludeFile(Root);
        var excludes = CoverageWorkspace.ReadExcludes(excludeFile, []);
        IReadOnlyList<CoverageRepo> discovered;
        if (PlatformMode)
        {
            var slnx = CoverageWorkspace.ResolvePlatformSlnx(Root);
            discovered = TestHostDiscovery.DiscoverFromPlatformSlnx(Root, slnx, excludes, include: null);
        }
        else
        {
            discovered = TestHostDiscovery.DiscoverRepos(Root, excludes, include: null);
        }

        var previous = Repos.ToDictionary(r => r.Name, r => r.IsSelected, StringComparer.OrdinalIgnoreCase);
        Repos = discovered
            .Select(r => new RepoListItem
            {
                Name = r.Name,
                Path = r.Path,
                HostCount = r.TestProjects.Count,
                Solution = r.Solution,
                IsSelected = previous.TryGetValue(r.Name, out var sel) ? sel : true,
            })
            .ToList();
        AppendLog($"Discovered {Repos.Count} repo(s) ({(PlatformMode ? "Platform.slnx" : "NuGet per-repo")}).");
        RaiseChanged();
    }

    public IReadOnlyList<string> SelectedRepoNames() =>
        Repos.Where(r => r.IsSelected).Select(r => r.Name).ToList();

    public void SelectAll(bool selected)
    {
        foreach (var r in Repos)
            r.IsSelected = selected;
        RaiseChanged();
    }

    public async Task CollectCoverageAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureRoot();
        var include = SelectedRepoNames();
        if (include.Count == 0)
            throw new InvalidOperationException("Select at least one repo.");

        using var linked = LinkToken(cancellationToken);
        _active = true;
        RaiseChanged();
        try
        {
            Directory.CreateDirectory(OutputDir);
            AppendLog($"Collect coverage → {OutputDir}");
            progress?.Report("Collecting coverage…");

            var writer = new CallbackTextWriter(line =>
            {
                AppendLog(line);
                progress?.Report(line);
            });

            var collector = new CoverageCollector(writer);
            var result = await collector.CollectAsync(new CoverageCollectOptions
            {
                Root = Root,
                OutputDir = OutputDir,
                PlatformSlnx = PlatformMode,
                SkipBuild = SkipBuild,
                RegenerateSlnx = RegenerateSlnx,
                FailBelow = FailBelow,
                ThrottleLimit = ThrottleLimit,
                Include = include,
                FlattenHtml = true,
            }, linked.Token).ConfigureAwait(false);

            LastCollect = result;
            HtmlIndexPath = result.HtmlIndexPath;
            TryLoadMergedCobertura();
            AppendLog(
                $"Done in {result.DurationSeconds:0.0}s — line {Fmt(result.AggregateLinePercent)}% / branch {Fmt(result.AggregateBranchPercent)}%.");
            if (result.GateFailed)
                AppendLog($"Gate: {result.GateMessage}");
        }
        finally
        {
            _active = false;
            ClearRunToken();
            RaiseChanged();
        }
    }

    public async Task RunTestsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureRoot();
        var selected = Repos.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
            throw new InvalidOperationException("Select at least one repo.");

        using var linked = LinkToken(cancellationToken);
        _active = true;
        RaiseChanged();
        try
        {
            var useProjectRef = PlatformMode;
            var throttle = ThrottleLimit > 0 ? ThrottleLimit : Math.Max(1, Environment.ProcessorCount - 1);
            using var gate = new SemaphoreSlim(throttle, throttle);
            var tasks = selected.Select(async repo =>
            {
                await gate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    foreach (var proj in Directory.EnumerateFiles(Path.Combine(repo.Path, "tests"), "*.csproj", SearchOption.AllDirectories))
                    {
                        if (!TestHostDiscovery.IsTestHostProject(proj))
                            continue;

                        linked.Token.ThrowIfCancellationRequested();
                        var leaf = Path.GetFileNameWithoutExtension(proj);
                        AppendLog($"test {repo.Name}/{leaf}");
                        progress?.Report($"Testing {repo.Name}/{leaf}…");

                        var args = new List<string>
                        {
                            "test", proj,
                            "-c", "Debug",
                            $"-p:NovolisUseProjectReferences={(useProjectRef ? "true" : "false")}",
                        };
                        if (SkipBuild)
                            args.Add("--no-build");

                        var (code, output) = await RunDotnetAsync(args, linked.Token).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(output))
                            AppendLog(output.TrimEnd());
                        if (code != 0)
                            AppendLog($"FAIL {repo.Name}/{leaf} exit {code}");
                    }
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            AppendLog("Test run finished.");
        }
        finally
        {
            _active = false;
            ClearRunToken();
            RaiseChanged();
        }
    }

    public void AnalyzeCrap()
    {
        EnsureRoot();
        AppendLog("Analyzing CRAP / complexity…");
        LastCrap = CrapAnalyzer.AnalyzePlatform(new CrapAnalyzeOptions
        {
            Root = Root,
            CoverageDir = OutputDir,
            Threshold = CrapThreshold,
            Include = SelectedRepoNames().Count == Repos.Count ? [] : SelectedRepoNames(),
        });
        AppendLog(
            $"CRAP: {LastCrap.Methods.Count} methods, {LastCrap.FlaggedCount} flagged (threshold {CrapThreshold}), max {LastCrap.MaxScore:0.##}.");
        RaiseChanged();
    }

    public void LoadCobertura(string path)
    {
        LastDocument = CoberturaDocumentParser.Load(path);
        AppendLog(
            $"Loaded Cobertura {path} — line {LastDocument.Summary.LinePercent:0.0}% / branch {LastDocument.Summary.BranchPercent:0.0}%.");
        RaiseChanged();
    }

    public void SetHtmlIndexPath(string? path) => HtmlIndexPath = path;

    public void Cancel()
    {
        _runCts?.Cancel();
        AppendLog("Cancel requested.");
    }

    public void ClearLog()
    {
        lock (_logGate)
            _log.Clear();
        LogChanged?.Invoke();
    }

    public IReadOnlyList<CoverageRepoRow> RepoRows()
    {
        if (LastCollect is null)
            return [];
        return LastCollect.Repos.Select(r => new CoverageRepoRow
        {
            Repo = r.Repo,
            Status = r.Status,
            Seconds = r.Seconds,
            TestsTotal = r.TestsTotal,
            TestsPassed = r.TestsPassed,
            TestsFailed = r.TestsFailed,
            LinePercent = r.LinePercent,
            BranchPercent = r.BranchPercent,
            Error = r.Error,
        }).ToList();
    }

    public IReadOnlyList<PackageGapRow> GapRows(double target = 95, int take = 80)
    {
        if (LastDocument is null)
            return [];
        return CoverageAnalyzer.PackagesBelowTarget(LastDocument, target, take)
            .Select(p => new PackageGapRow
            {
                Package = p.Name,
                LinePercent = p.LinePercent,
                BranchPercent = p.BranchPercent,
                LineGap = p.LineGap,
                BranchGap = p.BranchGap,
            })
            .ToList();
    }

    public IReadOnlyList<ComplexityRow> ComplexityRows(bool flaggedOnly, int take = 500)
    {
        if (LastCrap is null)
            return [];
        var q = flaggedOnly ? LastCrap.Methods.Where(m => m.Flagged) : LastCrap.Methods.AsEnumerable();
        return q.Take(take).Select(m => new ComplexityRow
        {
            Crap = Math.Round(m.Score, 2),
            Complexity = m.Method.Complexity,
            LinePercent = m.Method.LinePercent,
            BranchPercent = m.Method.BranchPercent,
            Package = m.Method.PackageName,
            Method = m.Method.DisplayName,
            File = string.IsNullOrWhiteSpace(m.Method.FileName)
                ? null
                : Path.GetFileName(m.Method.FileName),
            Flagged = m.Flagged,
        }).ToList();
    }

    public JobQueueRow BuildJobRow(string title)
    {
        return new JobQueueRow
        {
            Title = title,
            StatusLabel = _active ? "Running" : "Idle",
            Detail = LastCollect is null
                ? $"{Repos.Count} repos · {(PlatformMode ? "Platform" : "NuGet")}"
                : $"line {Fmt(LastCollect.AggregateLinePercent)}% · branch {Fmt(LastCollect.AggregateBranchPercent)}%",
            LogTail = Tail(LogText, 4000),
            CanCancel = _active,
            CanOpenOutput = !string.IsNullOrWhiteSpace(HtmlIndexPath) && File.Exists(HtmlIndexPath!),
            Progress = null,
            Tag = this,
        };
    }

    private void TryLoadMergedCobertura()
    {
        var candidates = new[]
        {
            Path.Combine(OutputDir, "Cobertura.xml"),
            Path.Combine(OutputDir, "report", "Cobertura.xml"),
        };
        foreach (var c in candidates)
        {
            if (!File.Exists(c))
                continue;
            LastDocument = CoberturaDocumentParser.Load(c);
            return;
        }
    }

    private void EnsureRoot()
    {
        if (string.IsNullOrWhiteSpace(Root) || !Directory.Exists(Root))
            throw new InvalidOperationException("Set a valid Novolis workspace root.");
        if (string.IsNullOrWhiteSpace(OutputDir))
            OutputDir = Path.Combine(Root, "coverage");
    }

    private CancellationTokenSource LinkToken(CancellationToken external)
    {
        _runCts?.Dispose();
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(external);
        return _runCts;
    }

    private void ClearRunToken()
    {
        _runCts?.Dispose();
        _runCts = null;
    }

    private void AppendLog(string line)
    {
        lock (_logGate)
        {
            _log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ");
            _log.AppendLine(line);
        }
        LogChanged?.Invoke();
    }

    private void RaiseChanged() => Changed?.Invoke();

    private static string TryResolveRoot()
    {
        try
        {
            return CoverageWorkspace.ResolveRoot();
        }
        catch
        {
            return @"d:\novolis";
        }
    }

    private static string Fmt(double? value) =>
        value is { } v ? v.ToString("0.0") : "—";

    private static string Tail(string text, int max)
    {
        if (text.Length <= max)
            return text;
        return text[^max..];
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var combined = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
        return (process.ExitCode, combined);
    }

    private sealed class CallbackTextWriter(Action<string> onLine) : TextWriter
    {
        private readonly StringBuilder _buf = new();
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value is '\n' or '\r')
                FlushLine();
            else
                _buf.Append(value);
        }

        public override void Write(string? value)
        {
            if (value is null)
                return;
            foreach (var ch in value)
                Write(ch);
        }

        public override void Flush() => FlushLine();

        private void FlushLine()
        {
            if (_buf.Length == 0)
                return;
            var line = _buf.ToString();
            _buf.Clear();
            if (!string.IsNullOrWhiteSpace(line))
                onLine(line.TrimEnd());
        }
    }
}
