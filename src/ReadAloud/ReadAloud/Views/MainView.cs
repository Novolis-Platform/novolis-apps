using System.Collections;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Novolis.Avalonia.Ndjson;
using Novolis.Avalonia.Mobile;
using Novolis.Avalonia.Speech;
using Novolis.Avalonia.Diagnostics;
using Novolis.Commands.Navigation;
using Novolis.IO.Ndjson;
using Novolis.Logging.Diagnostics;
using ReadAloud.Reading;
using ReadAloud.Services;
using ReadAloud.Ui;

namespace ReadAloud.Views;

/// <summary>Paste or open text, listen with Azure Speech, or save an MP3.</summary>
public sealed class MainView : DockPanel
{
    readonly SpeechService _speech;
    readonly SpeechWorkspace _workspace;
    readonly SpeechSettingsPanel _speechSettings;
    readonly IScreenWakeLock _wakeLock;
    readonly IDiagnosticShare _diagnosticShare;
    readonly IDiagnosticJournal _journal;
    readonly ILogger<MainView> _logger;
    readonly PassageSurface _passage;
    readonly Button _listenButton;
    readonly Button _openButton;
    readonly Button _pasteButton;
    readonly Button _saveButton;
    readonly Button _clearButton;
    readonly TextBlock _status;
    readonly TextBlock _connectionTitle;
    readonly TextBlock _connectionDetail;
    readonly Button _connectionAction;
    readonly Border _connectionCard;
    readonly Border _azureSetupPanel;
    readonly Services.IAzureSpeechResourcePicker? _resourcePicker;
    readonly Button _automaticModeButton;
    readonly Button _manualModeButton;
    readonly StackPanel _automaticCredentialsPanel;
    readonly StackPanel _manualCredentialsPanel;
    readonly TextBlock _setupProgress;
    readonly Button _signInButton;
    readonly Button _importButton;
    readonly Button _importEncryptedButton;
    readonly Button _exportCredentialsButton;
    readonly Button _signOutButton;
    readonly TextBlock _resourceStatus;
    readonly Button _usageRefreshButton;
    readonly TextBlock _deviceUsage;
    readonly TextBlock _usageStatus;
    readonly TextBlock _diagnosticsView;
    readonly Button _openDiagnosticsButton;
    readonly NdjsonSliceView _diagnosticRecords;
    readonly NdjsonFileReader _ndjsonReader = new();
    INdjsonDocument? _diagnosticDocument;
    readonly ComboBox _voicePicker;
    readonly TextBox _endpointBox;
    readonly TextBox _keyBox;
    readonly TextBox _credentialPassphraseBox;
    readonly Button _saveCredentialsButton;
    readonly ComboBox _subscriptionPicker;
    readonly ComboBox _resourceGroupPicker;
    readonly ComboBox _speechResourcePicker;
    readonly StackPanel _subscriptionStep;
    readonly StackPanel _resourceGroupStep;
    readonly StackPanel _speechResourceStep;
    IReadOnlyList<AzureSubscriptionChoice> _subscriptions = [];
    IReadOnlyList<AzureSpeechResourceGroupChoice> _resourceGroups = [];
    IReadOnlyList<AzureSpeechResourceChoice> _speechResources = [];
    IReadOnlyList<VoiceListItem> _voices = [];
    readonly MobileNavigationShell _navigationShell;
    bool _useAutomaticCredentials;
    bool _resourceRefresh;
    bool _voiceRefresh;
    bool _usageRefresh;
    bool _azureUsageLoaded;
    bool _listenPending;
    bool _signInBusy;
    bool _openedSetupForFirstRun;
    IDisposable? _wake;

    public MainView(
        SpeechService speech,
        SpeechWorkspace workspace,
        IScreenWakeLock wakeLock,
        IDiagnosticShare diagnosticShare,
        IDiagnosticJournal journal,
        ILogger<MainView> logger,
        Services.IAzureSpeechResourcePicker? resourcePicker = null)
    {
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _wakeLock = wakeLock ?? throw new ArgumentNullException(nameof(wakeLock));
        _diagnosticShare = diagnosticShare ?? throw new ArgumentNullException(nameof(diagnosticShare));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resourcePicker = resourcePicker
            ?? workspace.ResourcePicker as Services.IAzureSpeechResourcePicker;
        _useAutomaticCredentials = _resourcePicker is not null;
        _speechSettings = new SpeechSettingsPanel { Workspace = _workspace };

        LastChildFill = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = GraphicalProfile.BackgroundBrush;

        _passage = new PassageSurface();
        _listenButton = ReadAloudTheme.Button("Listen", ReadAloudButtonKind.Primary);
        _listenButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _listenButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _listenButton.MinHeight = 52;
        _listenButton.Click += async (_, _) => await OnListenClickAsync();
        _openButton = ReadAloudTheme.Button("Open", ReadAloudButtonKind.Secondary);
        _openButton.Click += async (_, _) => await OpenFileAsync();
        _pasteButton = ReadAloudTheme.Button("Paste", ReadAloudButtonKind.Secondary);
        _pasteButton.Click += async (_, _) => await PasteClipboardAsync();
        _saveButton = ReadAloudTheme.Button("Save MP3", ReadAloudButtonKind.Secondary);
        _saveButton.Click += async (_, _) => await SaveMp3Async();
        _clearButton = ReadAloudTheme.Button("Clear", ReadAloudButtonKind.Quiet);
        _clearButton.Click += (_, _) => ClearPassage();

        _connectionTitle = ReadAloudTheme.Heading(CredentialSummary.Title(null), 16);
        _connectionDetail = ReadAloudTheme.Muted(CredentialSummary.Detail(null), 13);
        _connectionAction = ReadAloudTheme.Button("Connect", ReadAloudButtonKind.Primary);
        _connectionAction.Click += (_, _) => OnConnectionActionClick();
        _connectionCard = BuildConnectionCard();

        var diagnosticsBtn = ReadAloudTheme.Button(_diagnosticShare.ActionLabel, ReadAloudButtonKind.Quiet);
        diagnosticsBtn.Click += async (_, _) => await ShareDiagnosticsAsync();

        var secondary = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 0),
        };
        foreach (var btn in new[] { _openButton, _pasteButton, _saveButton, _clearButton })
        {
            btn.Margin = new Thickness(0, 0, 8, 8);
            secondary.Children.Add(btn);
        }

        _voicePicker = ReadAloudTheme.Picker();
        _voicePicker.IsEnabled = false;
        ResetVoicePickerCore();
        _voicePicker.SelectionChanged += async (_, _) => await OnVoiceSelectedAsync();

        _status = ReadAloudTheme.Muted("Paste text, then tap Listen.");
        ReadAloudTheme.ApplyStatusTone(_status, StatusTone.Neutral);

        _automaticModeButton = ReadAloudTheme.Button("Microsoft account", ReadAloudButtonKind.Secondary);
        _automaticModeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _automaticModeButton.Click += (_, _) => SetCredentialMode(automatic: true);
        _manualModeButton = ReadAloudTheme.Button("Endpoint & key", ReadAloudButtonKind.Secondary);
        _manualModeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _manualModeButton.Click += (_, _) => SetCredentialMode(automatic: false);

        _signInButton = ReadAloudTheme.Button("Sign in with Microsoft", ReadAloudButtonKind.Primary);
        _signInButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _signInButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _signInButton.MinHeight = 48;
        _signInButton.Click += async (_, _) => await SignInAzureAsync();
        _endpointBox = ReadAloudTheme.Field("https://your-resource.cognitiveservices.azure.com/");
        _keyBox = ReadAloudTheme.Field("Subscription key", secret: true);
        _saveCredentialsButton = ReadAloudTheme.Button("Save and connect", ReadAloudButtonKind.Primary);
        _saveCredentialsButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _saveCredentialsButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _saveCredentialsButton.Click += async (_, _) => await SaveManualCredentialsAsync();
        _importButton = ReadAloudTheme.Button("Import JSON…", ReadAloudButtonKind.Secondary);
        _importButton.Click += async (_, _) => await ImportAzureCredentialsAsync();
        _importEncryptedButton = ReadAloudTheme.Button("Import encrypted…", ReadAloudButtonKind.Secondary);
        _importEncryptedButton.Click += async (_, _) => await ImportEncryptedAzureCredentialsAsync();
        _exportCredentialsButton = ReadAloudTheme.Button("Export encrypted…", ReadAloudButtonKind.Secondary);
        _exportCredentialsButton.Click += async (_, _) => await ExportAzureCredentialsAsync();
        _credentialPassphraseBox = ReadAloudTheme.Field(
            "Backup passphrase (8+ characters)",
            secret: true);
        _signOutButton = ReadAloudTheme.Button("Sign out", ReadAloudButtonKind.Quiet);
        _signOutButton.Click += async (_, _) => await SignOutAzureAsync();
        _signOutButton.IsVisible = false;
        _setupProgress = ReadAloudTheme.Muted("Step 1 of 3 — Sign in", 13);
        _resourceStatus = ReadAloudTheme.Muted(
            "After sign-in, choose the Speech resource to use.",
            13);
        _deviceUsage = ReadAloudTheme.Muted(_speech.DeviceUsageSummary, 13);
        _usageStatus = ReadAloudTheme.Muted(
            "Sign in and select a Speech service to load the last 30 days.",
            13);
        _diagnosticsView = ReadAloudTheme.Muted(
            "Diagnostics appear here after listen, save, and usage refresh.",
            12);
        _openDiagnosticsButton = ReadAloudTheme.Button(
            "Browse full diagnostic journal",
            ReadAloudButtonKind.Secondary);
        _openDiagnosticsButton.Click += async (_, _) => await OpenDiagnosticsAsync();
        _diagnosticRecords = new NdjsonSliceView
        {
            IsVisible = false,
            ErrorHandler = (_, error) =>
            {
                SetStatus($"Diagnostic journal unavailable: {error.Message}", StatusTone.Warning);
                return Task.CompletedTask;
            },
        };
        _usageRefreshButton = ReadAloudTheme.Button("Refresh usage", ReadAloudButtonKind.Secondary);
        _usageRefreshButton.IsEnabled = false;
        _usageRefreshButton.Click += async (_, _) => await RefreshUsageAsync();

        _subscriptionPicker = ReadAloudTheme.Picker();
        _subscriptionPicker.IsEnabled = false;
        _subscriptionPicker.SelectionChanged += async (_, _) =>
            await OnSubscriptionSelectedAsync();
        _resourceGroupPicker = ReadAloudTheme.Picker();
        _resourceGroupPicker.IsEnabled = false;
        _resourceGroupPicker.SelectionChanged += async (_, _) =>
            await OnResourceGroupSelectedAsync();
        _speechResourcePicker = ReadAloudTheme.Picker();
        _speechResourcePicker.IsEnabled = false;
        _speechResourcePicker.SelectionChanged += async (_, _) =>
            await OnSpeechResourceSelectedAsync();

        _subscriptionStep = BuildPickerStep("Azure subscription", _subscriptionPicker);
        _resourceGroupStep = BuildPickerStep("Resource group", _resourceGroupPicker);
        _speechResourceStep = BuildPickerStep("Speech service", _speechResourcePicker);
        _subscriptionStep.IsVisible = false;
        _resourceGroupStep.IsVisible = false;
        _speechResourceStep.IsVisible = false;

        _automaticCredentialsPanel = BuildAutomaticCredentialsPanel();
        _manualCredentialsPanel = BuildManualCredentialsPanel();
        _azureSetupPanel = BuildAzureSetupPanel();
        RefreshCredentialVariant();

        var passageCard = ReadAloudTheme.Card(_passage, new Thickness(12));
        passageCard.MinHeight = 180;
        passageCard.VerticalAlignment = VerticalAlignment.Stretch;

        var passageScroll = new ScrollViewer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(16, 8, 16, 8),
            Content = passageCard,
        };

        var details = new Expander
        {
            Header = "Usage and diagnostics",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 8, 0, 0),
                Children =
                {
                    ReadAloudTheme.SectionLabel("This device"),
                    _deviceUsage,
                    ReadAloudTheme.SectionLabel("Azure usage — last 30 days"),
                    _usageStatus,
                    _usageRefreshButton,
                    ReadAloudTheme.SectionLabel("Last operation"),
                    _diagnosticsView,
                    _openDiagnosticsButton,
                    _diagnosticRecords,
                    diagnosticsBtn,
                },
            },
        };

        var header = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(16, 12, 16, 0),
            Children =
            {
                _speechSettings,
            },
        };
        var footer = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16, 0, 16, 12),
            Children =
            {
                _status,
                _listenButton,
                secondary,
                details,
            },
        };

        var body = new DockPanel
        {
            LastChildFill = true,
        };
        SetDock(header, Dock.Top);
        SetDock(footer, Dock.Bottom);
        body.Children.Add(header);
        body.Children.Add(footer);
        body.Children.Add(passageScroll);

        var menuTree = new MobileMenuTree(
        [
            new MobileMenuNode("actions", "Actions", MobileMenuNodeKind.Group)
            {
                Children =
                [
                    new MobileMenuNode("text.open", "Open text file", MobileMenuNodeKind.Action)
                    {
                        CommandId = "text.open",
                    },
                    new MobileMenuNode("text.paste", "Paste from clipboard", MobileMenuNodeKind.Action)
                    {
                        CommandId = "text.paste",
                    },
                    new MobileMenuNode("audio.save", "Save MP3", MobileMenuNodeKind.Action)
                    {
                        CommandId = "audio.save",
                    },
                    new MobileMenuNode("text.clear", "Clear passage", MobileMenuNodeKind.Action)
                    {
                        CommandId = "text.clear",
                    },
                    new MobileMenuNode("azure.settings", "Azure Speech connection", MobileMenuNodeKind.Action)
                    {
                        CommandId = "azure.settings",
                    },
                    new MobileMenuNode("credentials", "Credentials", MobileMenuNodeKind.Group)
                    {
                        Children =
                        [
                            new MobileMenuNode(
                                "credentials.import-json",
                                "Import JSON credentials",
                                MobileMenuNodeKind.Action)
                            {
                                CommandId = "credentials.import-json",
                            },
                            new MobileMenuNode(
                                "credentials.import-encrypted",
                                "Import encrypted backup",
                                MobileMenuNodeKind.Action)
                            {
                                CommandId = "credentials.import-encrypted",
                            },
                            new MobileMenuNode(
                                "credentials.export-encrypted",
                                "Export encrypted backup",
                                MobileMenuNodeKind.Action)
                            {
                                CommandId = "credentials.export-encrypted",
                            },
                        ],
                    },
                    new MobileMenuNode("diagnostics.share", "Share diagnostics", MobileMenuNodeKind.Action)
                    {
                        CommandId = "diagnostics.share",
                    },
                    new MobileMenuNode("diagnostics.open", "Browse diagnostics", MobileMenuNodeKind.Action)
                    {
                        CommandId = "diagnostics.open",
                    },
                ],
            },
        ]);
        _navigationShell = new MobileNavigationShell(new MobileNavigationController(menuTree))
        {
            Title = "Read Aloud",
            Body = body,
        };
        _navigationShell.ActivationRequested += OnNavigationActivationRequested;
        Children.Add(_navigationShell);

        _speech.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshListen);
        _workspace.Changed += (_, _) => Dispatcher.UIThread.Post(OnWorkspaceChanged);
        SharedTextInbox.Received += text =>
            Dispatcher.UIThread.Post(() => ApplySharedText(text));
        BaselineLaunch.Requested += path =>
            Dispatcher.UIThread.Post(() => _ = RunBaselineAsync(path));
        DetachedFromVisualTree += (_, _) =>
        {
            _speech.Stop();
            ReleaseWake();
            _ = CloseDiagnosticDocumentAsync();
        };

        if (SharedTextInbox.TryTake(out var shared))
            ApplySharedText(shared);

        RefreshProviderUi();
        RefreshListen();
        _ = InitializeSpeechAsync();
    }

    /// <summary>Closes the mobile menu before the Android activity navigates back.</summary>
    public bool TryHandleBack()
    {
        if (_workspace.WantAzureSetup && _speech.CanCreateMp3)
        {
            _workspace.DismissAzureSetup();
            return true;
        }

        return _navigationShell.TryHandleBack();
    }

    void OnWorkspaceChanged()
    {
        _speech.SetVoice(_workspace.Voice.Voice);
        RefreshListen();
        if (CanRefreshAzureUsage() && !_azureUsageLoaded)
            _ = RefreshUsageAsync();
    }

    void OnNavigationActivationRequested(
        object? sender,
        MobileMenuActivationEventArgs args)
    {
        _ = HandleNavigationActivationAsync(args.CommandId);
    }

    async Task HandleNavigationActivationAsync(string? commandId)
    {
        switch (commandId)
        {
            case "text.open":
                await OpenFileAsync();
                break;
            case "text.paste":
                await PasteClipboardAsync();
                break;
            case "audio.save":
                await SaveMp3Async();
                break;
            case "text.clear":
                ClearPassage();
                break;
            case "azure.settings":
                await _workspace.SelectAzureAsync();
                break;
            case "credentials.import-json":
                _workspace.SetAzureCredentialMode(automatic: false);
                await ImportAzureCredentialsAsync();
                break;
            case "credentials.import-encrypted":
                _workspace.SetAzureCredentialMode(automatic: false);
                await ImportEncryptedAzureCredentialsAsync();
                break;
            case "credentials.export-encrypted":
                _workspace.SetAzureCredentialMode(automatic: false);
                await ExportAzureCredentialsAsync();
                break;
            case "diagnostics.share":
                await ShareDiagnosticsAsync();
                break;
            case "diagnostics.open":
                await OpenDiagnosticsAsync();
                break;
        }
    }

    string DocumentText => _passage.Text;

    async Task InitializeSpeechAsync()
    {
        try
        {
            await _workspace.InitializeAsync();
            _speech.SetVoice(_workspace.Voice.Voice);
            Dispatcher.UIThread.Post(() =>
            {
                RefreshManualCredentialFields();
                RefreshProviderUi();
                RefreshListen();
            });
            Dispatcher.UIThread.Post(() =>
            {
                RefreshDeviceUsage();
                RefreshDiagnosticsView();
                RefreshConnectionCard();
                if (_speech.CanCreateMp3)
                {
                    SetStatus(
                        _speech.AzureConfiguration is null
                            ? "Ready (Edge Read Aloud). Listen starts at the beginning."
                            : "Ready. Listen starts at the beginning.",
                        StatusTone.Success);
                }
                else if (!_openedSetupForFirstRun && !_workspace.Capabilities.CanCreateMp3)
                {
                    _openedSetupForFirstRun = true;
                    // Edge should already be selected by SpeechWorkspace; only warn if still blocked.
                    SetStatus(
                        "Speech is not ready yet. Pick Edge, or connect Azure.",
                        StatusTone.Warning);
                }

                if (CanRefreshAzureUsage())
                    _ = RefreshUsageAsync();
            });
            if (BaselineLaunch.TryTake(out var baselinePath))
                await RunBaselineAsync(baselinePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud speech setup load failed.");
            Dispatcher.UIThread.Post(() =>
            {
                SetStatus(
                    AzureErrorText.Describe(ex, "Azure Speech setup needs attention"),
                    StatusTone.Danger);
                _ = _workspace.SelectAzureAsync();
                RefreshProviderUi();
                RefreshListen();
            });
        }
    }

    Border BuildConnectionCard()
    {
        var actionRow = new DockPanel
        {
            LastChildFill = true,
        };
        SetDock(_connectionAction, Dock.Right);
        _connectionAction.Margin = new Thickness(12, 0, 0, 0);
        actionRow.Children.Add(_connectionAction);
        actionRow.Children.Add(new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _connectionTitle,
                _connectionDetail,
            },
        });

        return ReadAloudTheme.Card(actionRow, new Thickness(14, 12));
    }

    Border BuildAzureSetupPanel()
    {
        var modeRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,8,*"),
            IsVisible = _resourcePicker is not null,
        };
        Grid.SetColumn(_automaticModeButton, 0);
        Grid.SetColumn(_manualModeButton, 2);
        modeRow.Children.Add(_automaticModeButton);
        modeRow.Children.Add(_manualModeButton);

        var close = ReadAloudTheme.Button("Close", ReadAloudButtonKind.Quiet);
        close.Click += (_, _) => TryCloseAzureSetup();

        var setupHeader = new DockPanel
        {
            LastChildFill = true,
        };
        SetDock(close, Dock.Right);
        setupHeader.Children.Add(close);
        setupHeader.Children.Add(ReadAloudTheme.Heading("Azure Speech connection", 17));

        var children = new List<Control>
        {
            setupHeader,
            ReadAloudTheme.Muted(
                _resourcePicker is not null
                    ? "Use your Microsoft account to pick a Speech resource, or paste an endpoint and key."
                    : "Paste the Speech endpoint and subscription key for your Azure resource.",
                13),
        };

        if (_resourcePicker is not null)
        {
            children.Add(ReadAloudTheme.SectionLabel("How to connect"));
            children.Add(modeRow);
            children.Add(_automaticCredentialsPanel);
        }

        children.Add(_manualCredentialsPanel);
        var content = new StackPanel
        {
            Spacing = 10,
        };
        foreach (var child in children)
            content.Children.Add(child);

        var panel = ReadAloudTheme.Card(content, new Thickness(14));
        panel.IsVisible = false;
        panel.Margin = new Thickness(0, 4, 0, 0);
        GraphicalProfileBinding.Bind(
            panel,
            Border.BorderBrushProperty,
            GraphicalProfile.AccentResourceKey);
        return panel;
    }

    StackPanel BuildAutomaticCredentialsPanel()
    {
        var actions = new WrapPanel();
        foreach (var button in new[] { _signOutButton })
        {
            button.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(button);
        }

        return new StackPanel
        {
            Spacing = 10,
            IsVisible = false,
            Children =
            {
                _setupProgress,
                ReadAloudTheme.Muted(
                    "No Speech key is stored. Microsoft keeps the sign-in on this device.",
                    13),
                _signInButton,
                actions,
                _resourceStatus,
                _subscriptionStep,
                _resourceGroupStep,
                _speechResourceStep,
            },
        };
    }

    StackPanel BuildManualCredentialsPanel()
    {
        var test = ReadAloudTheme.Button("Test connection", ReadAloudButtonKind.Secondary);
        test.Click += async (_, _) => await TestAzureAsync();
        var remove = ReadAloudTheme.Button("Remove connection", ReadAloudButtonKind.Danger);
        remove.Click += async (_, _) => await RemoveAzureSetupAsync();
        var primaryActions = new WrapPanel();
        foreach (var button in new[] { test, remove })
        {
            button.Margin = new Thickness(0, 0, 8, 8);
            primaryActions.Children.Add(button);
        }

        var backupActions = new WrapPanel();
        foreach (var button in new[]
                 {
                     _importButton,
                     _importEncryptedButton,
                     _exportCredentialsButton,
                 })
        {
            button.Margin = new Thickness(0, 0, 8, 8);
            backupActions.Children.Add(button);
        }

        var advanced = new Expander
        {
            Header = "Import, export, and backup",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 0),
                Children =
                {
                    ReadAloudTheme.Muted(
                        "Import a JSON credential file, or move a saved key between devices with an encrypted backup passphrase.",
                        13),
                    ReadAloudTheme.SectionLabel("Encrypted backup passphrase"),
                    _credentialPassphraseBox,
                    backupActions,
                },
            },
        };

        return new StackPanel
        {
            IsVisible = false,
            Spacing = 8,
            Children =
            {
                ReadAloudTheme.Muted(
                    "The key stays in platform secure storage. Azure Monitor usage needs Microsoft sign-in.",
                    13),
                ReadAloudTheme.SectionLabel("Endpoint"),
                _endpointBox,
                ReadAloudTheme.SectionLabel("Subscription key"),
                _keyBox,
                _saveCredentialsButton,
                primaryActions,
                advanced,
            },
        };
    }

    static StackPanel BuildPickerStep(string label, ComboBox picker) =>
        new()
        {
            Spacing = 4,
            Children =
            {
                ReadAloudTheme.SectionLabel(label),
                picker,
            },
        };

    void OnConnectionActionClick()
    {
        if (_azureSetupPanel.IsVisible)
        {
            TryCloseAzureSetup();
            return;
        }

        ToggleAzureSetup(forceOpen: true);
    }

    void TryCloseAzureSetup()
    {
        if (!_speech.CanCreateMp3)
        {
            SetStatus("Connect Azure Speech before listening.", StatusTone.Warning);
            return;
        }

        _azureSetupPanel.IsVisible = false;
        RefreshConnectionCard();
    }

    void ToggleAzureSetup(bool forceOpen = false)
    {
        if (forceOpen)
            _azureSetupPanel.IsVisible = true;
        else
            _azureSetupPanel.IsVisible = !_azureSetupPanel.IsVisible;

        if (!_azureSetupPanel.IsVisible)
        {
            RefreshConnectionCard();
            return;
        }

        var setup = _speech.AzureConfiguration;
        if (_resourcePicker is not null && setup is not null)
            _useAutomaticCredentials =
                setup.EffectiveCredentialSource == AzureSpeechCredentialSource.Automatic;

        RefreshManualCredentialFields();
        RefreshCredentialVariant();
        SetStatus(
            _useAutomaticCredentials && _resourcePicker is not null
                ? "Sign in, then choose a Speech resource."
                : "Enter the Speech endpoint and key, or import a credential file.",
            StatusTone.Neutral);
        RefreshConnectionCard();
    }

    void SetCredentialMode(bool automatic)
    {
        if (automatic && _resourcePicker is null)
            return;

        _useAutomaticCredentials = automatic;
        RefreshCredentialVariant();
        SetStatus(
            automatic
                ? "Sign in with Microsoft to choose a Speech resource."
                : "Enter the Speech endpoint and subscription key.",
            StatusTone.Neutral);
    }

    void RefreshCredentialVariant()
    {
        var automatic = _resourcePicker is not null && _useAutomaticCredentials;
        _automaticCredentialsPanel.IsVisible = automatic;
        _manualCredentialsPanel.IsVisible = !automatic;
        if (_resourcePicker is not null)
        {
            ReadAloudTheme.StyleModeToggle(_automaticModeButton, automatic);
            ReadAloudTheme.StyleModeToggle(_manualModeButton, !automatic);
        }

        var setup = _speech.AzureConfiguration;
        _signOutButton.IsVisible = _resourcePicker is not null
            && (setup is { EffectiveCredentialSource: AzureSpeechCredentialSource.Automatic }
                || _subscriptions.Count > 0);
        _exportCredentialsButton.IsEnabled = setup is
        {
            EffectiveCredentialSource: AzureSpeechCredentialSource.Manual,
            AuthenticationMode: AzureSpeechAuthenticationMode.ApiKey,
        };
        _importEncryptedButton.IsEnabled = true;
        RefreshSetupProgress();

        var usageReady = CanRefreshAzureUsage();
        _usageRefreshButton.IsEnabled = usageReady && !_usageRefresh;
        if (!usageReady)
        {
            _azureUsageLoaded = false;
            _usageStatus.Text = _resourcePicker is null
                ? "Azure Monitor totals need Microsoft sign-in on Android. This device still tracks local characters, calls, and failures."
                : automatic
                    ? "Sign in and select a Speech service to load the last 30 days."
                    : "Azure Monitor needs Microsoft sign-in. A subscription key can speak, but cannot read management metrics.";
        }
        else if (!_azureUsageLoaded && !_usageRefresh)
        {
            _usageStatus.Text = "Tap Refresh usage to load the last 30 days.";
        }
    }

    void RefreshSetupProgress()
    {
        if (!_useAutomaticCredentials || _resourcePicker is null)
            return;

        if (_signInBusy)
        {
            _setupProgress.Text = "Opening Microsoft sign-in…";
            _resourceStatus.Text = "Pick the Microsoft account that owns your Azure Speech resource.";
            return;
        }

        if (_subscriptions.Count == 0)
        {
            _setupProgress.Text = "Step 1 of 3 — Sign in";
            _resourceStatus.Text = "After sign-in, Read Aloud lists compatible Speech services.";
            _subscriptionStep.IsVisible = false;
            _resourceGroupStep.IsVisible = false;
            _speechResourceStep.IsVisible = false;
            return;
        }

        if (_subscriptionPicker.SelectedIndex < 0)
        {
            _setupProgress.Text = "Step 2 of 3 — Choose subscription";
            _resourceStatus.Text = $"Signed in. {_subscriptions.Count:N0} subscription(s) available.";
            _subscriptionStep.IsVisible = true;
            _resourceGroupStep.IsVisible = false;
            _speechResourceStep.IsVisible = false;
            return;
        }

        if (_resourceGroups.Count == 0)
        {
            _setupProgress.Text = "Step 2 of 3 — Looking for Speech resources…";
            return;
        }

        if (_resourceGroupPicker.SelectedIndex < 0 && _resourceGroups.Count > 1)
        {
            _setupProgress.Text = "Step 2 of 3 — Choose resource group";
            _resourceStatus.Text = $"{_resourceGroups.Count:N0} resource groups contain Speech services.";
            _subscriptionStep.IsVisible = true;
            _resourceGroupStep.IsVisible = true;
            _speechResourceStep.IsVisible = false;
            return;
        }

        if (_speechResources.Count == 0)
        {
            _setupProgress.Text = "Step 3 of 3 — Looking for Speech services…";
            _subscriptionStep.IsVisible = true;
            _resourceGroupStep.IsVisible = _resourceGroups.Count > 1;
            _speechResourceStep.IsVisible = false;
            return;
        }

        if (_speechResourcePicker.SelectedIndex < 0 && _speechResources.Count > 1)
        {
            _setupProgress.Text = "Step 3 of 3 — Choose Speech service";
            _resourceStatus.Text = $"{_speechResources.Count:N0} Speech services found.";
            _subscriptionStep.IsVisible = true;
            _resourceGroupStep.IsVisible = _resourceGroups.Count > 1;
            _speechResourceStep.IsVisible = true;
            return;
        }

        _setupProgress.Text = "Connected";
        _resourceStatus.Text = "Speech resource selected. You can close this panel and listen.";
        _subscriptionStep.IsVisible = true;
        _resourceGroupStep.IsVisible = _resourceGroups.Count > 1;
        _speechResourceStep.IsVisible = _speechResources.Count > 1;
    }

    bool CanRefreshAzureUsage()
    {
        var setup = _speech.AzureConfiguration;
        return _resourcePicker is not null &&
               _useAutomaticCredentials &&
               setup is { EffectiveCredentialSource: AzureSpeechCredentialSource.Automatic };
    }

    void RefreshDeviceUsage() => _deviceUsage.Text = _speech.DeviceUsageSummary;

    void RefreshDiagnosticsView()
    {
        var operation = _speech.LastOperation is null
            ? null
            : OperationReportText.Format(_speech.LastOperation);
        var summary = _journal.ReadRecentSummary();
        if (!string.IsNullOrWhiteSpace(operation) && !string.IsNullOrWhiteSpace(summary))
            _diagnosticsView.Text = operation + Environment.NewLine + Environment.NewLine + summary;
        else if (!string.IsNullOrWhiteSpace(operation))
            _diagnosticsView.Text = operation;
        else
            _diagnosticsView.Text = string.IsNullOrWhiteSpace(summary)
                ? "No diagnostic events yet."
                : summary;
    }

    async Task RefreshUsageAsync()
    {
        if (_resourcePicker is null)
        {
            _usageStatus.Text =
                "Azure Monitor usage is available on Android with Microsoft sign-in.";
            return;
        }

        var setup = _speech.AzureConfiguration;
        if (setup is null ||
            setup.EffectiveCredentialSource != AzureSpeechCredentialSource.Automatic)
        {
            _usageStatus.Text =
                "Switch to Microsoft account sign-in to view Azure Monitor usage.";
            return;
        }

        if (_usageRefresh)
            return;

        _usageRefresh = true;
        RefreshCredentialVariant();
        _usageStatus.Text = "Loading Azure Monitor usage…";
        try
        {
            var usage = await _resourcePicker
                .GetUsageAsync(setup.Endpoint)
                .ConfigureAwait(true);
            _usageStatus.Text = FormatUsage(usage);
            _azureUsageLoaded = true;
            _logger.LogInformation(
                "Azure usage loaded. Characters {Characters}. Calls {Calls}. Successful {Successful}. ClientErrors {ClientErrors}. ServerErrors {ServerErrors}.",
                usage.SynthesizedCharacters,
                usage.TotalCalls,
                usage.SuccessfulCalls,
                usage.ClientErrors,
                usage.ServerErrors);
        }
        catch (OperationCanceledException)
        {
            _azureUsageLoaded = true;
            _usageStatus.Text = "Azure usage lookup cancelled.";
        }
        catch (Exception ex)
        {
            _azureUsageLoaded = true;
            _logger.LogError(ex, "Read Aloud Azure usage lookup failed.");
            _usageStatus.Text = AzureErrorText.Describe(ex, "Azure usage unavailable");
        }
        finally
        {
            _usageRefresh = false;
            RefreshCredentialVariant();
            RefreshDiagnosticsView();
        }
    }

    static string FormatUsage(AzureSpeechUsageSnapshot usage)
    {
        var values = new List<string>();
        if (usage.SynthesizedCharacters is { } characters)
            values.Add($"Synthesized characters: {characters:N0}");
        if (usage.TotalCalls is { } calls)
            values.Add($"Calls: {calls:N0}");
        if (usage.SuccessfulCalls is { } successful)
            values.Add($"Successful: {successful:N0}");

        var errors = (usage.ClientErrors ?? 0) + (usage.ServerErrors ?? 0);
        if (usage.ClientErrors is not null || usage.ServerErrors is not null)
            values.Add($"Errors: {errors:N0}");

        if (values.Count == 0)
            values.Add("No Azure Monitor data was returned.");

        var text = string.Join(" · ", values) +
            $"\nWindow: {usage.Start.UtcDateTime:yyyy-MM-dd} – " +
            $"{usage.End.UtcDateTime:yyyy-MM-dd} UTC";
        if (!string.IsNullOrWhiteSpace(usage.Notice))
            text += $"\n{usage.Notice}";
        return text;
    }

    async Task SignInAzureAsync()
    {
        if (_resourcePicker is null)
            return;

        _signInBusy = true;
        _signInButton.IsEnabled = false;
        _signInButton.Content = "Waiting for Microsoft…";
        RefreshSetupProgress();
        SetStatus("Opening Microsoft sign-in…", StatusTone.Busy);
        try
        {
            _subscriptions = await _resourcePicker.SignInAsync();
            _signOutButton.IsVisible = true;
            _resourceRefresh = true;
            _subscriptionPicker.ItemsSource =
                _subscriptions.Select(subscription => subscription.DisplayName).ToArray();
            _subscriptionPicker.SelectedIndex = _subscriptions.Count == 1 ? 0 : -1;
            _subscriptionPicker.IsEnabled = _subscriptions.Count > 0;
            ResetResourcePickersCore();
            _resourceRefresh = false;

            if (_subscriptions.Count == 0)
            {
                RefreshSetupProgress();
                SetStatus(
                    "No enabled Azure subscriptions are available on this account.",
                    StatusTone.Warning);
                return;
            }

            if (_subscriptions.Count == 1)
            {
                await LoadResourceGroupsAsync(_subscriptions[0]);
                return;
            }

            RefreshSetupProgress();
            SetStatus("Choose an Azure subscription.", StatusTone.Neutral);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Microsoft sign-in was cancelled.", StatusTone.Warning);
            RefreshSetupProgress();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure sign-in failed.");
            SetStatus(AzureErrorText.Describe(ex, "Azure sign-in failed"), StatusTone.Danger);
            RefreshSetupProgress();
        }
        finally
        {
            _signInBusy = false;
            _resourceRefresh = false;
            _signInButton.IsEnabled = true;
            _signInButton.Content = _subscriptions.Count > 0
                ? "Sign in again"
                : "Sign in with Microsoft";
            RefreshSetupProgress();
        }
    }

    async Task ImportAzureCredentialsAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Azure Speech credentials",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Azure Speech credentials JSON")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
            ],
        });
        if (files.Count == 0)
            return;

        _importButton.IsEnabled = false;
        SetStatus("Reading credentials…", StatusTone.Busy);
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var setup = await AzureSpeechCredentialFile.ReadAsync(
                stream,
                _speech.Voice.Voice,
                LocaleFromVoice(_speech.Voice.Voice));
            var voices = await _speech.TestAzureSetupAsync(setup);
            await _speech.ConfigureAzureAsync(setup);
            _endpointBox.Text = setup.Endpoint.AbsoluteUri;
            _keyBox.Text = string.Empty;
            _useAutomaticCredentials = false;
            _azureSetupPanel.IsVisible = false;
            RefreshCredentialVariant();
            await RefreshVoiceListAsync();
            SetStatus(
                $"Connected ({voices.Count:N0} voices available).",
                StatusTone.Success);
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud manual Azure Speech credential import failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Credential import failed"),
                StatusTone.Danger);
        }
        finally
        {
            _importButton.IsEnabled = true;
        }
    }

    async Task ImportEncryptedAzureCredentialsAsync()
    {
        var passphrase = _credentialPassphraseBox.Text;
        if (string.IsNullOrWhiteSpace(passphrase)
            || passphrase.Length < AzureSpeechCredentialBundle.MinimumPassphraseLength)
        {
            SetStatus(
                $"Enter the backup passphrase ({AzureSpeechCredentialBundle.MinimumPassphraseLength}+ characters).",
                StatusTone.Warning);
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import encrypted Azure Speech backup",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Encrypted Azure Speech backup")
                {
                    Patterns = ["*.json", "*.credentials"],
                    MimeTypes = ["application/json"],
                },
            ],
        });
        if (files.Count == 0)
            return;

        _importEncryptedButton.IsEnabled = false;
        SetStatus("Decrypting credentials…", StatusTone.Busy);
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var setup = await _speech.ReadAzureCredentialsAsync(stream, passphrase);
            var voices = await _speech.TestAzureSetupAsync(setup);
            await _speech.ConfigureAzureAsync(setup);
            _endpointBox.Text = setup.Endpoint.AbsoluteUri;
            _keyBox.Text = string.Empty;
            _credentialPassphraseBox.Text = string.Empty;
            _useAutomaticCredentials = false;
            _azureSetupPanel.IsVisible = false;
            RefreshCredentialVariant();
            await RefreshVoiceListAsync();
            SetStatus(
                $"Encrypted backup imported ({voices.Count:N0} voices available).",
                StatusTone.Success);
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud encrypted Azure Speech credential import failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Encrypted import failed"),
                StatusTone.Danger);
        }
        finally
        {
            _importEncryptedButton.IsEnabled = true;
        }
    }

    async Task ExportAzureCredentialsAsync()
    {
        var setup = _speech.AzureConfiguration;
        if (setup is null
            || setup.EffectiveCredentialSource != AzureSpeechCredentialSource.Manual
            || setup.AuthenticationMode != AzureSpeechAuthenticationMode.ApiKey)
        {
            SetStatus(
                "Encrypted export is available after a manual endpoint-and-key connection.",
                StatusTone.Warning);
            return;
        }

        var passphrase = _credentialPassphraseBox.Text;
        if (string.IsNullOrWhiteSpace(passphrase)
            || passphrase.Length < AzureSpeechCredentialBundle.MinimumPassphraseLength)
        {
            SetStatus(
                $"Enter an export passphrase ({AzureSpeechCredentialBundle.MinimumPassphraseLength}+ characters).",
                StatusTone.Warning);
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export encrypted Azure Speech credentials",
            SuggestedFileName = "readaloud-azure-credentials.json",
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("Encrypted Azure Speech credentials")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
            ],
        });
        if (file is null)
            return;

        _exportCredentialsButton.IsEnabled = false;
        SetStatus("Encrypting credentials…", StatusTone.Busy);
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await _speech.ExportAzureCredentialsAsync(stream, passphrase);
            _credentialPassphraseBox.Text = string.Empty;
            SetStatus($"Encrypted credentials exported to {file.Name}.", StatusTone.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud encrypted Azure Speech credential export failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Credential export failed"),
                StatusTone.Danger);
        }
        finally
        {
            _exportCredentialsButton.IsEnabled = true;
            RefreshCredentialVariant();
        }
    }

    async Task OnSubscriptionSelectedAsync()
    {
        if (_resourceRefresh ||
            _resourcePicker is null ||
            _subscriptionPicker.SelectedIndex < 0 ||
            _subscriptionPicker.SelectedIndex >= _subscriptions.Count)
        {
            return;
        }

        await LoadResourceGroupsAsync(_subscriptions[_subscriptionPicker.SelectedIndex]);
    }

    async Task LoadResourceGroupsAsync(AzureSubscriptionChoice subscription)
    {
        if (_resourcePicker is null)
            return;

        try
        {
            SetStatus($"Finding Speech resources in {subscription.DisplayName}…", StatusTone.Busy);
            RefreshSetupProgress();
            _resourceGroups = await _resourcePicker.GetSpeechResourceGroupsAsync(subscription.Id);
            _resourceRefresh = true;
            _resourceGroupPicker.ItemsSource =
                _resourceGroups
                    .Select(group => $"{group.Name} ({group.SpeechResourceCount:N0})")
                    .ToArray();
            _resourceGroupPicker.SelectedIndex = _resourceGroups.Count == 1 ? 0 : -1;
            _resourceGroupPicker.IsEnabled = _resourceGroups.Count > 0;
            ResetSpeechResourcePickerCore();
            _resourceRefresh = false;

            if (_resourceGroups.Count == 0)
            {
                RefreshSetupProgress();
                SetStatus(
                    "No compatible Azure Speech resource was found in that subscription.",
                    StatusTone.Warning);
                _resourceStatus.Text =
                    "No resource group contains a compatible Speech service.";
                return;
            }

            if (_resourceGroups.Count == 1)
            {
                await LoadSpeechResourcesAsync(subscription, _resourceGroups[0]);
                return;
            }

            RefreshSetupProgress();
            SetStatus("Choose a resource group.", StatusTone.Neutral);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Resource discovery cancelled.", StatusTone.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure resource-group discovery failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Resource discovery failed"),
                StatusTone.Danger);
        }
        finally
        {
            _resourceRefresh = false;
            RefreshSetupProgress();
        }
    }

    async Task OnResourceGroupSelectedAsync()
    {
        if (_resourceRefresh ||
            _resourcePicker is null ||
            _subscriptionPicker.SelectedIndex < 0 ||
            _resourceGroupPicker.SelectedIndex < 0 ||
            _resourceGroupPicker.SelectedIndex >= _resourceGroups.Count)
        {
            return;
        }

        await LoadSpeechResourcesAsync(
            _subscriptions[_subscriptionPicker.SelectedIndex],
            _resourceGroups[_resourceGroupPicker.SelectedIndex]);
    }

    async Task LoadSpeechResourcesAsync(
        AzureSubscriptionChoice subscription,
        AzureSpeechResourceGroupChoice resourceGroup)
    {
        if (_resourcePicker is null)
            return;

        try
        {
            SetStatus($"Finding Speech services in {resourceGroup.Name}…", StatusTone.Busy);
            RefreshSetupProgress();
            _speechResources = await _resourcePicker.GetSpeechResourcesAsync(
                subscription.Id,
                resourceGroup.Name);
            _resourceRefresh = true;
            _speechResourcePicker.ItemsSource =
                _speechResources
                    .Select(resource => $"{resource.Name} ({resource.Location})")
                    .ToArray();
            _speechResourcePicker.SelectedIndex = _speechResources.Count == 1 ? 0 : -1;
            _speechResourcePicker.IsEnabled = _speechResources.Count > 0;
            _resourceRefresh = false;

            if (_speechResources.Count == 0)
            {
                RefreshSetupProgress();
                SetStatus(
                    "No compatible Speech service was found in that group.",
                    StatusTone.Warning);
                return;
            }

            if (_speechResources.Count == 1)
            {
                await ConfigureAzureResourceAsync(_speechResources[0]);
                return;
            }

            RefreshSetupProgress();
            SetStatus("Choose a Speech service.", StatusTone.Neutral);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Resource discovery cancelled.", StatusTone.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure Speech discovery failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Speech discovery failed"),
                StatusTone.Danger);
        }
        finally
        {
            _resourceRefresh = false;
            RefreshSetupProgress();
        }
    }

    async Task OnSpeechResourceSelectedAsync()
    {
        if (_resourceRefresh ||
            _speechResourcePicker.SelectedIndex < 0 ||
            _speechResourcePicker.SelectedIndex >= _speechResources.Count)
        {
            return;
        }

        await ConfigureAzureResourceAsync(_speechResources[_speechResourcePicker.SelectedIndex]);
    }

    async Task ConfigureAzureResourceAsync(AzureSpeechResourceChoice resource)
    {
        try
        {
            SetStatus($"Connecting to {resource.Name}…", StatusTone.Busy);
            await _speech.ConfigureAzureAsync(new AzureSpeechSetup
            {
                Endpoint = resource.Endpoint,
                CredentialSource = AzureSpeechCredentialSource.Automatic,
                AuthenticationMode = AzureSpeechAuthenticationMode.MicrosoftEntra,
                ClientId = resource.ClientId,
                TenantId = resource.TenantId,
                VoiceName = _speech.Voice.Voice,
                Locale = LocaleFromVoice(_speech.Voice.Voice),
            });
            _azureSetupPanel.IsVisible = false;
            await RefreshVoiceListAsync();
            SetStatus(
                $"Connected to {resource.Name} ({_voices.Count:N0} voices).",
                StatusTone.Success);
            RefreshProviderUi();
            RefreshListen();
            _usageStatus.Text = "Tap Refresh usage to load the last 30 days.";
            await RefreshUsageAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Azure connection cancelled.", StatusTone.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure Speech resource connection failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Azure connection failed"),
                StatusTone.Danger);
        }
        finally
        {
            RefreshSetupProgress();
        }
    }

    async Task SignOutAzureAsync()
    {
        if (_resourcePicker is null)
            return;

        try
        {
            await _resourcePicker.SignOutAsync();
            await _speech.RemoveAzureAsync();
            _subscriptions = [];
            _resourceGroups = [];
            _speechResources = [];
            _resourceRefresh = true;
            _subscriptionPicker.ItemsSource = Array.Empty<string>();
            _subscriptionPicker.SelectedIndex = -1;
            _subscriptionPicker.IsEnabled = false;
            ResetResourcePickersCore();
            _resourceRefresh = false;
            _signOutButton.IsVisible = false;
            _signInButton.Content = "Sign in with Microsoft";
            _keyBox.Text = string.Empty;
            RefreshManualCredentialFields();
            ResetVoicePickerCore();
            RefreshCredentialVariant();
            ToggleAzureSetup(forceOpen: true);
            SetStatus("Signed out. Connect Azure Speech to listen again.", StatusTone.Warning);
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure sign-out failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Azure sign-out failed"),
                StatusTone.Danger);
        }
    }

    void ResetResourcePickersCore()
    {
        _resourceGroupPicker.ItemsSource = Array.Empty<string>();
        _resourceGroupPicker.SelectedIndex = -1;
        _resourceGroupPicker.IsEnabled = false;
        ResetSpeechResourcePickerCore();
    }

    void ResetSpeechResourcePickerCore()
    {
        _speechResourcePicker.ItemsSource = Array.Empty<string>();
        _speechResourcePicker.SelectedIndex = -1;
        _speechResourcePicker.IsEnabled = false;
    }

    async Task SaveManualCredentialsAsync()
    {
        _saveCredentialsButton.IsEnabled = false;
        SetStatus("Saving credentials…", StatusTone.Busy);
        try
        {
            var setup = AzureSpeechCredentialFile.FromManualEntry(
                _endpointBox.Text ?? string.Empty,
                _keyBox.Text,
                _speech.Voice.Voice,
                LocaleFromVoice(_speech.Voice.Voice));
            var voices = await _speech.TestAzureSetupAsync(setup);
            await _speech.ConfigureAzureAsync(setup);
            _endpointBox.Text = setup.Endpoint.AbsoluteUri;
            _keyBox.Text = string.Empty;
            _useAutomaticCredentials = false;
            _azureSetupPanel.IsVisible = false;
            RefreshCredentialVariant();
            await RefreshVoiceListAsync();
            SetStatus(
                $"Connected ({voices.Count:N0} voices available).",
                StatusTone.Success);
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud manual Azure Speech credential save failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Credential save failed"),
                StatusTone.Danger);
        }
        finally
        {
            _saveCredentialsButton.IsEnabled = true;
        }
    }

    async Task OnVoiceSelectedAsync()
    {
        if (_voiceRefresh ||
            _voicePicker.SelectedItem is not VoiceListItem item ||
            string.Equals(item.ShortName, _speech.Voice.Voice, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await _speech.UpdateVoiceAsync(item.ShortName, item.Locale);
            SetStatus($"Voice set to {item.Display}.", StatusTone.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud voice selection failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Voice selection failed"),
                StatusTone.Danger);
        }
    }

    async Task RefreshVoiceListAsync()
    {
        if (!_speech.CanCreateMp3)
        {
            ResetVoicePickerCore();
            return;
        }

        try
        {
            var voices = await _speech.ListVoicesAsync().ConfigureAwait(true);
            var items = voices
                .Select(voice => new VoiceListItem(
                    voice.ShortName,
                    voice.Locale,
                    $"{voice.Locale} — {voice.LocalName} ({voice.Gender})"))
                .OrderBy(item => item.Locale, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Display, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = _speech.Voice.Voice;
            if (!items.Exists(item =>
                    string.Equals(item.ShortName, current, StringComparison.OrdinalIgnoreCase)))
            {
                var locale = _speech.AzureConfiguration?.Locale
                    ?? LocaleFromVoice(current);
                items.Insert(
                    0,
                    new VoiceListItem(current, locale, $"{locale} — {current}"));
            }

            if (!items.Exists(item =>
                    string.Equals(
                        item.ShortName,
                        SpeechService.DefaultVoiceName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                items.Insert(
                    0,
                    new VoiceListItem(
                        SpeechService.DefaultVoiceName,
                        SpeechService.DefaultLocale,
                        $"{SpeechService.DefaultLocale} — Ava (Female)"));
            }

            var selected = items.FindIndex(item =>
                string.Equals(item.ShortName, current, StringComparison.OrdinalIgnoreCase));
            ApplyVoiceItems(items, selected >= 0 ? selected : 0, enabled: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud voice list failed.");
            var current = new VoiceListItem(
                _speech.Voice.Voice,
                LocaleFromVoice(_speech.Voice.Voice),
                $"{LocaleFromVoice(_speech.Voice.Voice)} — {_speech.Voice.Voice}");
            ApplyVoiceItems([current], selectedIndex: 0, enabled: false);
        }
    }

    void ResetVoicePickerCore()
    {
        _voices = [];
        ApplyVoicePicker(
            new[] { "Connect Azure Speech to list voices" },
            selectedIndex: 0,
            enabled: false);
    }

    void ApplyVoiceItems(
        IReadOnlyList<VoiceListItem> items,
        int selectedIndex,
        bool enabled)
    {
        _voices = items;
        ApplyVoicePicker(items, selectedIndex, enabled);
    }

    void ApplyVoicePicker(IEnumerable items, int selectedIndex, bool enabled)
    {
        void apply()
        {
            _voiceRefresh = true;
            _voicePicker.ItemsSource = items;
            _voicePicker.SelectedIndex = selectedIndex;
            _voicePicker.IsEnabled = enabled;
            _voiceRefresh = false;
        }

        if (Dispatcher.UIThread.CheckAccess())
            apply();
        else
            Dispatcher.UIThread.Post(apply);
    }

    void RefreshManualCredentialFields()
    {
        var setup = _speech.AzureConfiguration;
        _keyBox.PlaceholderText = setup is { EffectiveCredentialSource: AzureSpeechCredentialSource.Manual }
            ? "New subscription key"
            : "Subscription key";
        if (setup is null)
        {
            if (string.IsNullOrWhiteSpace(_endpointBox.Text))
                _endpointBox.Text = string.Empty;
            return;
        }

        _endpointBox.Text = setup.Endpoint.AbsoluteUri;
        _keyBox.Text = string.Empty;
    }

    void RefreshConnectionCard()
    {
        var setup = _speech.AzureConfiguration;
        _connectionTitle.Text = CredentialSummary.Title(setup);
        _connectionDetail.Text = _azureSetupPanel.IsVisible && setup is null
            ? "Finish the steps below, then close this panel."
            : CredentialSummary.Detail(setup);
        _connectionAction.Content = setup is null
            ? (_azureSetupPanel.IsVisible ? "Hide setup" : "Connect")
            : (_azureSetupPanel.IsVisible ? "Hide" : "Change");
        ReadAloudTheme.StyleButton(
            _connectionAction,
            setup is null ? ReadAloudButtonKind.Primary : ReadAloudButtonKind.Secondary);
    }

    static string LocaleFromVoice(string voiceName)
    {
        var parts = voiceName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0]}-{parts[1]}"
            : SpeechService.DefaultLocale;
    }

    sealed record VoiceListItem(string ShortName, string Locale, string Display)
    {
        public override string ToString() => Display;
    }

    async Task TestAzureAsync()
    {
        try
        {
            SetStatus("Testing Azure Speech connection…", StatusTone.Busy);
            await _speech.TestAzureAsync();
            await RefreshVoiceListAsync();
            SetStatus(
                $"Azure Speech connected ({_voices.Count:N0} voices available).",
                StatusTone.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure connection test failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Azure test failed"),
                StatusTone.Danger);
        }
    }

    async Task RemoveAzureSetupAsync()
    {
        try
        {
            await _speech.RemoveAzureAsync();
            _keyBox.Text = string.Empty;
            RefreshManualCredentialFields();
            ResetVoicePickerCore();
            RefreshCredentialVariant();
            RefreshProviderUi();
            RefreshListen();
            ToggleAzureSetup(forceOpen: true);
            SetStatus("Connection removed. Connect again to listen.", StatusTone.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure setup removal failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Could not remove Azure setup"),
                StatusTone.Danger);
        }
    }

    void RefreshProviderUi()
    {
        RefreshConnectionCard();
        RefreshCredentialVariant();
    }

    void ClearPassage()
    {
        _speech.Stop();
        _passage.Text = string.Empty;
        SetStatus("Passage cleared.", StatusTone.Neutral);
        RefreshListen();
    }

    void ApplySharedText(string text)
    {
        if (_speech.IsSpeaking)
        {
            _speech.Stop();
            ReleaseWake();
        }

        _passage.Text = text;
        SetStatus("Shared text is ready to listen.", StatusTone.Success);
        RefreshListen();
    }

    async Task RunBaselineAsync(string credentialPath)
    {
        if (_listenPending)
            return;

        _listenPending = true;
        _wake = _wakeLock.Acquire("read-aloud-baseline");
        SetStatus("Baseline…", StatusTone.Busy);
        RefreshListen();
        try
        {
            var line = await BaselineRun.ExecuteAsync(
                _speech,
                credentialPath,
                passage => Dispatcher.UIThread.Post(() => _passage.Text = passage));
            _logger.LogInformation("{Baseline}", line);
            SetStatus(line, StatusTone.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud baseline failed with {ExceptionType}.", ex.GetType().Name);
            SetStatus($"Baseline failed ({ex.GetType().Name}).", StatusTone.Danger);
        }
        finally
        {
            _listenPending = false;
            ReleaseWake();
            RefreshListen();
            RefreshDiagnosticsView();
            RefreshConnectionCard();
        }
    }

    async Task OpenFileAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open text to read",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text / Markdown")
                {
                    Patterns = ["*.txt", "*.md", "*.markdown", "*.text"],
                    MimeTypes = ["text/plain", "text/markdown"],
                },
                new FilePickerFileType("All files")
                {
                    Patterns = ["*.*"],
                },
            ],
        });

        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        _passage.Text = await reader.ReadToEndAsync();
        var label = files[0].Name;
        if (string.IsNullOrWhiteSpace(label))
            label = files[0].TryGetLocalPath() ?? "Opened file";
        SetStatus($"Opened {label}", StatusTone.Success);
        RefreshListen();
    }

    async Task PasteClipboardAsync()
    {
        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clip is null)
            return;

        var text = await clip.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Clipboard is empty.", StatusTone.Warning);
            return;
        }

        _passage.Text = text;
        SetStatus("Pasted clipboard.", StatusTone.Success);
        RefreshListen();
    }

    async Task OnListenClickAsync()
    {
        if (_speech.IsSpeaking)
        {
            _speech.Stop();
            ReleaseWake();
            SetStatus("Ready. Listen starts at the beginning.", StatusTone.Neutral);
            RefreshListen();
            RefreshDiagnosticsView();
            return;
        }

        if (_listenPending)
            return;

        if (!_speech.CanCreateMp3)
        {
            await _workspace.SelectAzureAsync();
            SetStatus(
                "Pick Edge Read Aloud, or connect Azure Speech, before listening.",
                StatusTone.Warning);
            return;
        }

        var text = DocumentText;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Paste or type something first.", StatusTone.Warning);
            return;
        }

        _wake = _wakeLock.Acquire("read-aloud-listen");
        _listenPending = true;
        SetStatus("Synthesizing…", StatusTone.Busy);
        RefreshListen();
        try
        {
            await _speech.SpeakAsync(text);
            if (!_speech.IsSpeaking)
            {
                SetStatus(
                    _speech.HasCachedAudio(text) ? "Done. Audio is cached." : "Done.",
                    StatusTone.Success);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Ready. Listen starts at the beginning.", StatusTone.Neutral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud listening failed.");
            SetStatus(AzureErrorText.Describe(ex, "Listen failed"), StatusTone.Danger);
        }
        finally
        {
            _listenPending = false;
            ReleaseWake();
            RefreshListen();
            RefreshDiagnosticsView();
        }
    }

    async Task SaveMp3Async()
    {
        var text = DocumentText;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Paste or type something first.", StatusTone.Warning);
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        if (_speech.IsSpeaking)
            _speech.Stop();

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save MP3",
            SuggestedFileName = "read-aloud.mp3",
            DefaultExtension = "mp3",
            FileTypeChoices =
            [
                new FilePickerFileType("MP3 audio")
                {
                    Patterns = ["*.mp3"],
                    MimeTypes = ["audio/mpeg"],
                },
            ],
        });

        if (file is null)
            return;

        if (!_speech.CanCreateMp3)
        {
            await _workspace.SelectAzureAsync();
            SetStatus(
                "MP3 export needs Edge Read Aloud or a connected Azure Speech resource.",
                StatusTone.Warning);
            return;
        }

        _saveButton.IsEnabled = false;
        SetStatus("Writing MP3…", StatusTone.Busy);
        try
        {
            var mp3 = await _speech.SynthesizeDocumentMp3Async(text);
            if (mp3.Length == 0)
            {
                SetStatus("Nothing to save — Azure Speech returned empty audio.", StatusTone.Warning);
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(mp3);
            SetStatus($"Saved {file.Name} ({mp3.Length:N0} bytes).", StatusTone.Success);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Save cancelled.", StatusTone.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud MP3 export failed.");
            SetStatus(AzureErrorText.Describe(ex, "Save failed"), StatusTone.Danger);
        }
        finally
        {
            _saveButton.IsEnabled = true;
            RefreshListen();
            RefreshDiagnosticsView();
        }
    }

    void RefreshListen()
    {
        var speaking = _speech.IsSpeaking || _listenPending;
        _passage.Locked = speaking;
        if (speaking && _speech.Progress.Reading)
            SetStatus(_speech.Progress.StatusText, StatusTone.Busy);
        _listenButton.Content = speaking ? "Stop" : "Listen";
        ReadAloudTheme.StyleButton(_listenButton, ReadAloudButtonKind.Primary);
        _openButton.IsEnabled = !speaking;
        _pasteButton.IsEnabled = !speaking;
        _clearButton.IsEnabled = !speaking;
        _saveButton.IsEnabled = !speaking && _speech.CanCreateMp3;
        _connectionAction.IsEnabled = !speaking;
        RefreshDeviceUsage();
        RefreshProviderUi();
    }

    async Task ShareDiagnosticsAsync()
    {
        try
        {
            RefreshDiagnosticsView();
            await _diagnosticShare.ShareLatestAsync();
            SetStatus("Diagnostics are ready to share.", StatusTone.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud diagnostics export failed.");
            SetStatus(
                AzureErrorText.Describe(ex, "Diagnostics failed"),
                StatusTone.Danger);
        }
    }

    async Task OpenDiagnosticsAsync()
    {
        var path = _journal.GetRecentFiles().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("No diagnostic journal has been recorded yet.", StatusTone.Warning);
            return;
        }

        try
        {
            var document = await _ndjsonReader.OpenAsync(new FileInfo(path));
            var previous = _diagnosticDocument;
            _diagnosticDocument = document;
            await _diagnosticRecords.OpenAsync(document, Path.GetFileName(path));
            _diagnosticRecords.IsVisible = true;
            if (previous is not null)
                await previous.DisposeAsync();
            SetStatus($"Browsing {Path.GetFileName(path)}", StatusTone.Neutral);
        }
        catch (Exception ex)
        {
            SetStatus(
                AzureErrorText.Describe(ex, "Diagnostic journal unavailable"),
                StatusTone.Danger);
        }
    }

    async Task CloseDiagnosticDocumentAsync()
    {
        if (_diagnosticDocument is not { } document)
            return;
        _diagnosticDocument = null;
        _diagnosticRecords.IsVisible = false;
        await document.DisposeAsync();
    }

    void SetStatus(string text, StatusTone tone = StatusTone.Neutral)
    {
        _status.Text = text;
        ReadAloudTheme.ApplyStatusTone(_status, tone);
    }

    void ReleaseWake()
    {
        _wake?.Dispose();
        _wake = null;
    }
}
