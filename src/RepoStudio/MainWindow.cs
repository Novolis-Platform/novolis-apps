using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Novolis.Avalonia.Git;
using Novolis.IO.Git;

namespace RepoStudio;

internal sealed class MainWindow : Window
{
    readonly GitRepositoryService _git = new();
    readonly GitRepoVisualizer _repos = new();
    readonly GitBranchNavigator _branches = new();
    readonly GitStashPanel _stashes = new();
    readonly GitCommitGraphView _graph = new();
    readonly GitCommitDetailView _detail = new();
    readonly GitDiffView _diff = new();
    readonly GitWorkingTreeView _working = new();
    readonly GitActionBar _actions = new();
    readonly GitFetchAgeLabel _fetchAge = new();
    readonly TextBlock _status = new()
    {
        Text = "Ready",
        Margin = new Thickness(10, 4),
        VerticalAlignment = VerticalAlignment.Center,
    };
    readonly TextBlock _emptyHint = new()
    {
        Text = "Double-click a repository to open its graph, branches, stash, and working tree.",
        Margin = new Thickness(16),
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.75,
    };

    string _root = "";
    string? _openRepoPath;
    FetchScheduler? _scheduler;
    int _matrixGen;
    bool _busy;

    public MainWindow()
    {
        Title = "Repo Studio";
        Width = 1400;
        Height = 900;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(28, 30, 34));
        Foreground = Brushes.WhiteSmoke;

        _repos.RepoOpenRequested += (_, e) => _ = OpenRepoAsync(e.Repo);
        _actions.CommandRequested += (_, e) => _ = OnCommandAsync(e);
        _stashes.CommandRequested += (_, e) => _ = OnCommandAsync(e);
        _branches.RefActivated += (_, e) => _ = CheckoutAsync(e.Tip.Name);
        _graph.CommitSelected += (_, e) => _ = ShowCommitAsync(e.Node);

        Content = BuildLayout();
        Opened += (_, _) => _ = OnOpenedAsync();
        Closed += (_, _) => _scheduler?.Stop();
    }

    async Task OnOpenedAsync()
    {
        try
        {
            _root = await Task.Run(() => GitWorkspace.ResolveRoot()).ConfigureAwait(true);
            Title = $"Repo Studio — {_root}";
            Flash("Loading workspace…");
            // Do not await on the UI event path — paint shell immediately.
            _ = RefreshMatrixAsync(includeStashCount: false);

            _scheduler = new FetchScheduler(_git);
            _scheduler.CycleCompleted += (_, _) =>
                Dispatcher.UIThread.Post(() => _ = RefreshMatrixAsync(includeStashCount: false));
            _scheduler.Start(_root, TimeSpan.FromMinutes(10), delayBeforeFirst: true);
        }
        catch (Exception ex)
        {
            Flash(ex.Message);
        }
    }

    Control BuildLayout()
    {
        var tabs = new TabControl
        {
            ItemsSource = new TabItem[]
            {
                new() { Header = "Working tree", Content = _working },
                new() { Header = "Diff", Content = _diff },
                new() { Header = "Detail", Content = _detail },
            },
        };

        var left = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(8, 8, 4, 8),
        };
        left.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(4, 0, 4, 6),
            Children =
            {
                new TextBlock { Text = "Workspace", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                _fetchAge,
            },
        });
        Grid.SetRow(_repos, 1);
        _repos.VerticalAlignment = VerticalAlignment.Stretch;
        left.Children.Add(_repos);

        var mid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,160"),
            Margin = new Thickness(4, 8, 4, 8),
        };
        var branchHost = new DockPanel();
        branchHost.Children.Add(new TextBlock
        {
            Text = "Branches",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(4, 0, 4, 4),
            [DockPanel.DockProperty] = Dock.Top,
        });
        branchHost.Children.Add(_branches);
        mid.Children.Add(branchHost);
        var stashHost = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 58, 64)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 4, 0, 0),
            Child = _stashes,
            [Grid.RowProperty] = 1,
        };
        mid.Children.Add(stashHost);

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("*,220"),
            Margin = new Thickness(4, 8, 8, 8),
        };
        var graphHost = new DockPanel();
        graphHost.Children.Add(new TextBlock
        {
            Text = "History",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(4, 0, 4, 4),
            [DockPanel.DockProperty] = Dock.Top,
        });
        var graphLayer = new Grid();
        graphLayer.Children.Add(_emptyHint);
        graphLayer.Children.Add(_graph);
        graphHost.Children.Add(graphLayer);
        right.Children.Add(graphHost);
        Grid.SetRow(tabs, 1);
        right.Children.Add(tabs);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,220,*"),
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(mid, 1);
        Grid.SetColumn(right, 2);
        body.Children.Add(left);
        body.Children.Add(mid);
        body.Children.Add(right);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var toolbar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(36, 38, 44)),
            Padding = new Thickness(4, 2),
            Child = _actions,
        };
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        var statusBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(36, 38, 44)),
            Child = _status,
            [Grid.RowProperty] = 2,
        };
        root.Children.Add(statusBar);
        return root;
    }

    async Task RefreshMatrixAsync(bool includeStashCount = false)
    {
        if (string.IsNullOrEmpty(_root))
            return;

        var gen = Interlocked.Increment(ref _matrixGen);
        var root = _root;
        var git = _git;
        try
        {
            var matrix = await GitWorkspace.GetStatusMatrixAsync(
                root,
                git,
                includeStashCount: includeStashCount,
                parallel: 8,
                liteStatus: true).ConfigureAwait(false);

            if (gen != _matrixGen)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _repos.SetMatrix(matrix);
                var stamps = matrix.Repos
                    .Select(r => r.LastFetchAt)
                    .Where(t => t.HasValue)
                    .Select(t => t!.Value)
                    .ToArray();
                _fetchAge.SetLastFetch(stamps.Length == 0 ? null : stamps.Max());
                Flash($"{matrix.Summary.Git} repos · dirty {matrix.Summary.Dirty} · behind {matrix.Summary.Behind}");
            });

            // Second pass for stash counts — never blocks first paint.
            if (!includeStashCount)
                _ = RefreshMatrixAsync(includeStashCount: true);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Flash(ex.Message));
        }
    }

    async Task OpenRepoAsync(RepoEntry repo)
    {
        _openRepoPath = repo.Path;
        _emptyHint.IsVisible = false;
        Flash($"Opening {repo.Name}…");
        await RefreshOpenRepoAsync().ConfigureAwait(true);
        Flash($"Opened {repo.Name}");
    }

    async Task RefreshOpenRepoAsync()
    {
        if (_openRepoPath is null)
            return;

        var path = _openRepoPath;
        try
        {
            var snapshot = await Task.Run(() =>
            {
                var branches = _git.ListBranches(path);
                var stashes = _git.ListStashes(path);
                var graph = _git.GetCommitGraph(path, new CommitGraphOptions { MaxCount = 120 });
                var wt = _git.GetWorkingTree(path);
                return (branches, stashes, graph, wt);
            }).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _branches.SetBranches(snapshot.branches);
                _stashes.SetStashes(snapshot.stashes);
                _graph.SetGraph(snapshot.graph);
                _working.SetWorkingTree(snapshot.wt);
                _emptyHint.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Flash(ex.Message));
        }
    }

    async Task ShowCommitAsync(CommitNode? node)
    {
        if (_openRepoPath is null || node is null)
            return;
        var path = _openRepoPath;
        var sha = node.Sha;
        try
        {
            var (detail, diff) = await Task.Run(() =>
                (_git.GetCommitDetail(path, sha), _git.GetDiff(path, sha))).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _detail.SetDetail(detail);
                _diff.SetDiff(diff);
            });
        }
        catch (Exception ex)
        {
            Flash(ex.Message);
        }
    }

    async Task CheckoutAsync(string tipName)
    {
        if (_openRepoPath is null)
            return;
        var path = _openRepoPath;
        Flash($"Checkout {tipName}…");
        var result = await Task.Run(() => _git.Checkout(path, tipName)).ConfigureAwait(false);
        Flash(result.Message);
        if (result.Ok)
            await RefreshOpenRepoAsync().ConfigureAwait(true);
    }

    async Task OnCommandAsync(GitChromeCommandEventArgs e)
    {
        if (_busy && e.Command is not GitChromeCommand.Refresh)
        {
            Flash("Busy…");
            return;
        }

        _busy = true;
        try
        {
            switch (e.Command)
            {
                case GitChromeCommand.Refresh:
                    Flash("Refreshing…");
                    await RefreshMatrixAsync(includeStashCount: true).ConfigureAwait(true);
                    await RefreshOpenRepoAsync().ConfigureAwait(true);
                    break;
                case GitChromeCommand.Fetch:
                {
                    Flash("Fetching…");
                    var sel = _repos.GetSelection();
                    var repos = await Task.Run(() =>
                    {
                        if (sel.Selected.Count > 0)
                            return sel.Selected;
                        return GitWorkspace.SelectByNames(GitWorkspace.Discover(_root), null);
                    }).ConfigureAwait(false);
                    var batch = new GitWorkspaceBatch(_git);
                    var result = await batch.FetchAsync(repos, new BatchOptions { WorkspaceRoot = _root })
                        .ConfigureAwait(false);
                    Flash($"Fetch ok={result.Ok}");
                    _ = RefreshMatrixAsync(includeStashCount: false);
                    break;
                }
                case GitChromeCommand.Pull:
                {
                    var sel = _repos.GetSelection();
                    if (sel.Selected.Count == 0)
                    {
                        Flash("Select repos to pull.");
                        break;
                    }

                    Flash("Pulling…");
                    var batch = new GitWorkspaceBatch(_git);
                    var result = await batch.PullFfOnlyAsync(sel.Selected, new BatchOptions { WorkspaceRoot = _root })
                        .ConfigureAwait(false);
                    Flash($"Pull failures={result.Results.Count(r => r.Outcome == "failed")}");
                    _ = RefreshMatrixAsync(includeStashCount: false);
                    await RefreshOpenRepoAsync().ConfigureAwait(true);
                    break;
                }
                case GitChromeCommand.Push:
                    if (_openRepoPath is null) { Flash("Open a repo first."); break; }
                    {
                        var path = _openRepoPath;
                        var r = await Task.Run(() => _git.Push(path)).ConfigureAwait(false);
                        Flash(r.Message);
                    }
                    break;
                case GitChromeCommand.StashPush:
                case GitChromeCommand.StashApply:
                case GitChromeCommand.StashPop:
                case GitChromeCommand.StashDrop:
                    await StashAsync(e).ConfigureAwait(true);
                    break;
                case GitChromeCommand.CreateBranch:
                    await CreateBranchAsync().ConfigureAwait(true);
                    break;
                case GitChromeCommand.BranchCut:
                    await BranchCutAsync().ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception ex)
        {
            Flash(ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    async Task StashAsync(GitChromeCommandEventArgs e)
    {
        if (_openRepoPath is null)
        {
            Flash("Open a repo first.");
            return;
        }

        var path = _openRepoPath;
        var idx = e.StashIndex ?? 0;
        var r = await Task.Run(() => e.Command switch
        {
            GitChromeCommand.StashPush => _git.StashPush(path),
            GitChromeCommand.StashApply => _git.StashApply(path, idx),
            GitChromeCommand.StashPop => _git.StashPop(path, idx),
            _ => _git.StashDrop(path, idx),
        }).ConfigureAwait(false);
        Flash(r.Message);
        await RefreshOpenRepoAsync().ConfigureAwait(true);
    }

    async Task CreateBranchAsync()
    {
        if (_openRepoPath is null)
        {
            Flash("Open a repo first.");
            return;
        }

        var body = new GitCreateBranchDialog();
        var ok = await ShowDialogAsync("Create branch", body).ConfigureAwait(true);
        if (!ok)
            return;
        var opts = body.TryRead();
        if (opts is null)
        {
            Flash("Branch name required.");
            return;
        }

        var path = _openRepoPath;
        var r = await Task.Run(() => _git.CreateBranch(path, opts)).ConfigureAwait(false);
        Flash(r.Message);
        await RefreshOpenRepoAsync().ConfigureAwait(true);
    }

    async Task BranchCutAsync()
    {
        var sel = _repos.GetSelection();
        if (sel.Selected.Count == 0)
        {
            Flash("Select repos for branch cut.");
            return;
        }

        var body = new GitBranchCutDialog();
        var ok = await ShowDialogAsync("Branch cut", body).ConfigureAwait(true);
        if (!ok || string.IsNullOrWhiteSpace(body.BranchName))
            return;

        var planner = new BranchCutPlanner(_git);
        var plan = await Task.Run(() =>
            planner.Plan(_root, body.BranchName, sel.Selected, body.BaseRef)).ConfigureAwait(false);
        body.SetPreview(plan);
        var confirm = await ShowDialogAsync("Confirm apply", body).ConfigureAwait(true);
        if (!confirm)
            return;

        Flash("Applying branch cut…");
        var applied = await planner.ApplyAsync(plan, dryRun: false).ConfigureAwait(false);
        Flash($"Branch cut ok={applied.Ok}");
        _ = RefreshMatrixAsync(includeStashCount: false);
    }

    async Task<bool> ShowDialogAsync(string title, Control body)
    {
        var tcs = new TaskCompletionSource<bool>();
        Window? dlg = null;
        Button MakeButton(string label, bool result)
        {
            var b = new Button { Content = label, MinWidth = 80 };
            b.Click += (_, _) =>
            {
                tcs.TrySetResult(result);
                dlg?.Close();
            };
            return b;
        }

        dlg = new Window
        {
            Title = title,
            Width = 520,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(8),
                        [DockPanel.DockProperty] = Dock.Bottom,
                        Children =
                        {
                            MakeButton("Cancel", false),
                            MakeButton("OK", true),
                        },
                    },
                    body,
                },
            },
        };

        await dlg.ShowDialog(this).ConfigureAwait(true);
        return await tcs.Task.ConfigureAwait(true);
    }

    void Flash(string message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Flash(message));
            return;
        }

        _status.Text = message;
        _status.Foreground = Brushes.LightGray;
    }
}
