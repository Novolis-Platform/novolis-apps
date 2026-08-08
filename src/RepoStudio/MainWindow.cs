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
    readonly TextBlock _status = new() { Text = "Ready", Margin = new Thickness(8, 4) };

    string _root = "";
    string? _openRepoPath;
    FetchScheduler? _scheduler;

    public MainWindow()
    {
        Title = "Repo Studio";
        Width = 1400;
        Height = 900;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _repos.RepoOpenRequested += async (_, e) => await OpenRepoAsync(e.Repo);
        _actions.CommandRequested += async (_, e) => await OnCommandAsync(e);
        _stashes.CommandRequested += async (_, e) => await OnCommandAsync(e);
        _branches.RefActivated += async (_, e) =>
        {
            if (_openRepoPath is null) return;
            var r = _git.Checkout(_openRepoPath, e.Tip.Name);
            Flash(r.Message);
            await RefreshOpenRepoAsync();
        };
        _graph.CommitSelected += (_, e) =>
        {
            if (_openRepoPath is null || e.Node is null) return;
            try
            {
                _detail.SetDetail(_git.GetCommitDetail(_openRepoPath, e.Node.Sha));
                _diff.SetDiff(_git.GetDiff(_openRepoPath, e.Node.Sha));
            }
            catch (Exception ex)
            {
                Flash(ex.Message);
            }
        };

        Content = BuildLayout();
        Opened += async (_, _) =>
        {
            try
            {
                _root = GitWorkspace.ResolveRoot();
                Title = $"Repo Studio — {_root}";
                RefreshMatrix();
                _scheduler = new FetchScheduler(_git);
                _scheduler.CycleCompleted += (_, _) => Dispatcher.UIThread.Post(RefreshMatrix);
                _scheduler.Start(_root, TimeSpan.FromMinutes(10));
                Flash($"Workspace {_root}");
            }
            catch (Exception ex)
            {
                Flash(ex.Message);
            }

            await Task.CompletedTask;
        };
        Closed += (_, _) => _scheduler?.Stop();
    }

    Control BuildLayout()
    {
        var left = new DockPanel
        {
            Width = 320,
            Children =
            {
                new Border
                {
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { _fetchAge },
                    },
                    [DockPanel.DockProperty] = Dock.Top,
                    Margin = new Thickness(4),
                },
                _repos,
            },
        };

        var nav = new Grid
        {
            RowDefinitions = new RowDefinitions("*,160"),
            Width = 220,
            Children =
            {
                _branches,
                new Border { Child = _stashes, [Grid.RowProperty] = 1 },
            },
        };

        var center = new Grid
        {
            RowDefinitions = new RowDefinitions("*,180"),
            Children =
            {
                _graph,
                new TabControl
                {
                    [Grid.RowProperty] = 1,
                    Items =
                    {
                        new TabItem { Header = "Working tree", Content = _working },
                        new TabItem { Header = "Diff", Content = _diff },
                        new TabItem { Header = "Detail", Content = _detail },
                    },
                },
            },
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("320,220,*"),
            Children =
            {
                left,
                nav,
                center,
            },
        };
        Grid.SetColumn(nav, 1);
        Grid.SetColumn(center, 2);

        return new DockPanel
        {
            Children =
            {
                _actions.WithDock(Dock.Top),
                _status.WithDock(Dock.Bottom),
                body,
            },
        };
    }

    void RefreshMatrix()
    {
        if (string.IsNullOrEmpty(_root))
            return;
        var matrix = GitWorkspace.GetStatusMatrix(_root, _git, includeStashCount: true);
        _repos.SetMatrix(matrix);
        var anyFetch = matrix.Repos.Select(r => r.LastFetchAt).Where(t => t is not null).Max();
        _fetchAge.SetLastFetch(anyFetch);
    }

    async Task OpenRepoAsync(RepoEntry repo)
    {
        _openRepoPath = repo.Path;
        await RefreshOpenRepoAsync();
        Flash($"Opened {repo.Name}");
    }

    async Task RefreshOpenRepoAsync()
    {
        if (_openRepoPath is null)
            return;
        await Task.Run(() =>
        {
            var path = _openRepoPath!;
            var branches = _git.ListBranches(path);
            var stashes = _git.ListStashes(path);
            var graph = _git.GetCommitGraph(path, new CommitGraphOptions { MaxCount = 120 });
            var wt = _git.GetWorkingTree(path);
            Dispatcher.UIThread.Post(() =>
            {
                _branches.SetBranches(branches);
                _stashes.SetStashes(stashes);
                _graph.SetGraph(graph);
                _working.SetWorkingTree(wt);
            });
        });
    }

    async Task OnCommandAsync(GitChromeCommandEventArgs e)
    {
        try
        {
            switch (e.Command)
            {
                case GitChromeCommand.Refresh:
                    RefreshMatrix();
                    await RefreshOpenRepoAsync();
                    break;
                case GitChromeCommand.Fetch:
                {
                    var sel = _repos.GetSelection();
                    var repos = sel.Selected.Count > 0
                        ? sel.Selected
                        : GitWorkspace.SelectByNames(GitWorkspace.Discover(_root), null);
                    var batch = new GitWorkspaceBatch(_git);
                    var result = await batch.FetchAsync(repos, new BatchOptions { WorkspaceRoot = _root });
                    Flash($"Fetch ok={result.Ok}");
                    RefreshMatrix();
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

                    var batch = new GitWorkspaceBatch(_git);
                    var result = await batch.PullFfOnlyAsync(sel.Selected, new BatchOptions { WorkspaceRoot = _root });
                    Flash($"Pull failures={result.Results.Count(r => r.Outcome == "failed")}");
                    RefreshMatrix();
                    await RefreshOpenRepoAsync();
                    break;
                }
                case GitChromeCommand.Push:
                    if (_openRepoPath is null) { Flash("Open a repo first."); break; }
                    Flash(_git.Push(_openRepoPath).Message);
                    break;
                case GitChromeCommand.StashPush:
                    if (_openRepoPath is null) break;
                    Flash(_git.StashPush(_openRepoPath).Message);
                    await RefreshOpenRepoAsync();
                    break;
                case GitChromeCommand.StashApply:
                    if (_openRepoPath is null) break;
                    Flash(_git.StashApply(_openRepoPath, e.StashIndex ?? 0).Message);
                    await RefreshOpenRepoAsync();
                    break;
                case GitChromeCommand.StashPop:
                    if (_openRepoPath is null) break;
                    Flash(_git.StashPop(_openRepoPath, e.StashIndex ?? 0).Message);
                    await RefreshOpenRepoAsync();
                    break;
                case GitChromeCommand.StashDrop:
                    if (_openRepoPath is null) break;
                    Flash(_git.StashDrop(_openRepoPath, e.StashIndex ?? 0).Message);
                    await RefreshOpenRepoAsync();
                    break;
                case GitChromeCommand.CreateBranch:
                    await CreateBranchAsync();
                    break;
                case GitChromeCommand.BranchCut:
                    await BranchCutAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Flash(ex.Message);
        }
    }

    async Task CreateBranchAsync()
    {
        if (_openRepoPath is null)
        {
            Flash("Open a repo first.");
            return;
        }

        var body = new GitCreateBranchDialog();
        var ok = await ShowDialogAsync("Create branch", body);
        if (!ok)
            return;
        var opts = body.TryRead();
        if (opts is null)
        {
            Flash("Branch name required.");
            return;
        }

        Flash(_git.CreateBranch(_openRepoPath, opts).Message);
        await RefreshOpenRepoAsync();
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
        var planner = new BranchCutPlanner(_git);
        // Preview after user fills name: show dialog then plan on OK
        var ok = await ShowDialogAsync("Branch cut", body);
        if (!ok || string.IsNullOrWhiteSpace(body.BranchName))
            return;

        var plan = planner.Plan(_root, body.BranchName, sel.Selected, body.BaseRef);
        body.SetPreview(plan);
        var confirm = await ShowDialogAsync("Confirm dry-run → apply", body);
        if (!confirm)
            return;
        var dry = await planner.ApplyAsync(plan, dryRun: true);
        Flash($"Dry-run steps={dry.Results.Count}");
        var applied = await planner.ApplyAsync(plan, dryRun: false);
        Flash($"Branch cut ok={applied.Ok}");
        RefreshMatrix();
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

        await dlg.ShowDialog(this);
        return await tcs.Task;
    }

    void Flash(string message)
    {
        _status.Text = message;
        _status.Foreground = Brushes.LightGray;
    }
}

file static class DockExt
{
    public static T WithDock<T>(this T control, Dock dock) where T : Control
    {
        DockPanel.SetDock(control, dock);
        return control;
    }
}
