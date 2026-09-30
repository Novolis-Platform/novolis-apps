using System.Collections;
using Novolis.Avalonia.GraphicalProfile;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Novolis.Avalonia.Speech;
using Novolis.Avalonia.Diagnostics;
using Novolis.Logging.Diagnostics;
using ReadAloud.Services;
using ReadAloud.Ui;

namespace ReadAloud.Views;

/// <summary>Paste or open text, listen with Azure Speech, or save an MP3.</summary>
public sealed class MainView : DockPanel
{
    readonly SpeechService _speech;
    readonly IScreenWakeLock _wakeLock;
    readonly IDiagnosticShare _diagnosticShare;
    readonly IDiagnosticJournal _journal;
    readonly ILogger<MainView> _logger;
    readonly TextBox _textBox;
    readonly Button _listenButton;
    readonly Button _saveButton;
    readonly TextBlock _status;
    readonly Button _configureButton;
    readonly Border _azureSetupPanel;
    readonly IAzureSpeechResourcePicker? _resourcePicker;
    readonly ComboBox _credentialVariantPicker;
    readonly StackPanel _automaticCredentialsPanel;
    readonly StackPanel _manualCredentialsPanel;
    readonly Button _signInButton;
    readonly Button _importButton;
    readonly Button _signOutButton;
    readonly TextBlock _credentialStatus;
    readonly TextBlock _resourceStatus;
    readonly Button _usageRefreshButton;
    readonly TextBlock _deviceUsage;
    readonly TextBlock _usageStatus;
    readonly TextBlock _diagnosticsView;
    readonly ComboBox _voicePicker;
    readonly TextBox _endpointBox;
    readonly TextBox _keyBox;
    readonly Button _saveCredentialsButton;
    readonly ComboBox _subscriptionPicker;
    readonly ComboBox _resourceGroupPicker;
    readonly ComboBox _speechResourcePicker;
    IReadOnlyList<AzureSubscriptionChoice> _subscriptions = [];
    IReadOnlyList<AzureSpeechResourceGroupChoice> _resourceGroups = [];
    IReadOnlyList<AzureSpeechResourceChoice> _speechResources = [];
    IReadOnlyList<VoiceListItem> _voices = [];
    bool _resourceRefresh;
    bool _voiceRefresh;
    bool _usageRefresh;
    bool _azureUsageLoaded;
    bool _listenPending;
    IDisposable? _wake;

    public MainView(
        SpeechService speech,
        IScreenWakeLock wakeLock,
        IDiagnosticShare diagnosticShare,
        IDiagnosticJournal journal,
        ILogger<MainView> logger,
        IAzureSpeechResourcePicker? resourcePicker = null)
    {
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _wakeLock = wakeLock ?? throw new ArgumentNullException(nameof(wakeLock));
        _diagnosticShare = diagnosticShare ?? throw new ArgumentNullException(nameof(diagnosticShare));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resourcePicker = resourcePicker;

        LastChildFill = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;
        Background = GraphicalProfile.BackgroundBrush;

        _textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Paste or type anything to hear…",
            FontFamily = GraphicalProfile.BodyFont,
            FontSize = 16,
            Foreground = GraphicalProfile.TextBrush,
            Background = GraphicalProfile.RaisedBrush,
            CaretBrush = GraphicalProfile.AccentBrush,
        };

        var openBtn = ReadAloudTheme.Button("Open file…", ReadAloudButtonKind.Secondary);
        openBtn.Click += async (_, _) => await OpenFileAsync();
        var pasteBtn = ReadAloudTheme.Button("Paste", ReadAloudButtonKind.Secondary);
        pasteBtn.Click += async (_, _) => await PasteClipboardAsync();
        _listenButton = ReadAloudTheme.Button("Listen", ReadAloudButtonKind.Primary);
        _listenButton.Click += async (_, _) => await OnListenClickAsync();
        _saveButton = ReadAloudTheme.Button("Save MP3", ReadAloudButtonKind.Secondary);
        _saveButton.Click += async (_, _) => await SaveMp3Async();
        _configureButton = ReadAloudTheme.Button("Azure setup", ReadAloudButtonKind.Secondary);
        _configureButton.Click += (_, _) => ToggleAzureSetup();
        var diagnosticsBtn = ReadAloudTheme.Button(_diagnosticShare.ActionLabel, ReadAloudButtonKind.Quiet);
        diagnosticsBtn.Click += async (_, _) => await ShareDiagnosticsAsync();
        var clearBtn = ReadAloudTheme.Button("Clear", ReadAloudButtonKind.Quiet);
        clearBtn.Click += (_, _) => Clear();

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0),
        };
        foreach (var btn in new[] { openBtn, pasteBtn, _listenButton, _saveButton, diagnosticsBtn, clearBtn })
        {
            btn.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(btn);
        }

        _voicePicker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false,
            FontFamily = GraphicalProfile.BodyFont,
        };
        ResetVoicePickerCore();
        _voicePicker.SelectionChanged += async (_, _) => await OnVoiceSelectedAsync();

        var providerRow = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 4, 0, 0),
        };
        providerRow.Children.Add(ReadAloudTheme.Muted("Voice service: Azure Speech only", 14));
        providerRow.Children.Add(_configureButton);
        providerRow.Children.Add(ReadAloudTheme.Muted("Voice", 12));
        providerRow.Children.Add(_voicePicker);

        _status = ReadAloudTheme.Muted(
            "Azure Speech is required. Sign in or import credentials to begin.");

        _credentialVariantPicker = new ComboBox
        {
            ItemsSource = new[]
            {
                "Automatic — Microsoft sign-in",
                "Manual — JSON credentials file",
            },
            SelectedIndex = _resourcePicker is null ? 1 : 0,
            IsEnabled = _resourcePicker is not null,
            FontFamily = GraphicalProfile.BodyFont,
        };
        _credentialVariantPicker.SelectionChanged += (_, _) => RefreshCredentialVariant();
        _signInButton = ReadAloudTheme.Button(
            "Sign in with Microsoft",
            ReadAloudButtonKind.Primary);
        _signInButton.Click += async (_, _) => await SignInAzureAsync();
        _endpointBox = ReadAloudTheme.Field("https://your-resource.cognitiveservices.azure.com/");
        _keyBox = ReadAloudTheme.Field("Subscription key", secret: true);
        _saveCredentialsButton = ReadAloudTheme.Button(
            "Save credentials",
            ReadAloudButtonKind.Primary);
        _saveCredentialsButton.Click += async (_, _) => await SaveManualCredentialsAsync();
        _importButton = ReadAloudTheme.Button(
            "Import credentials file…",
            ReadAloudButtonKind.Secondary);
        _importButton.Click += async (_, _) => await ImportAzureCredentialsAsync();
        _signOutButton = ReadAloudTheme.Button("Sign out", ReadAloudButtonKind.Quiet);
        _signOutButton.Click += async (_, _) => await SignOutAzureAsync();
        _signOutButton.IsVisible = false;
        _credentialStatus = ReadAloudTheme.Muted(
            "No Azure Speech credentials are configured.",
            13);
        _resourceStatus = ReadAloudTheme.Muted(
            "Sign in to choose an Azure subscription.",
            13);
        _deviceUsage = ReadAloudTheme.Muted(_speech.DeviceUsageSummary, 13);
        _usageStatus = ReadAloudTheme.Muted(
            "Sign in and select an Azure Speech service to load the last 30 days.",
            13);
        _diagnosticsView = ReadAloudTheme.Muted("Diagnostics appear here after listen, save, and usage refresh.", 12);
        _usageRefreshButton = ReadAloudTheme.Button(
            "Refresh usage",
            ReadAloudButtonKind.Secondary);
        _usageRefreshButton.IsEnabled = false;
        _usageRefreshButton.Click += async (_, _) => await RefreshUsageAsync();
        _subscriptionPicker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false,
            FontFamily = GraphicalProfile.BodyFont,
        };
        _subscriptionPicker.SelectionChanged += async (_, _) =>
            await OnSubscriptionSelectedAsync();
        _resourceGroupPicker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
            IsEnabled = false,
            FontFamily = GraphicalProfile.BodyFont,
        };
        _resourceGroupPicker.SelectionChanged += async (_, _) =>
            await OnResourceGroupSelectedAsync();
        _speechResourcePicker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
            IsEnabled = false,
            FontFamily = GraphicalProfile.BodyFont,
        };
        _speechResourcePicker.SelectionChanged += async (_, _) =>
            await OnSpeechResourceSelectedAsync();
        _automaticCredentialsPanel = BuildAutomaticCredentialsPanel();
        _manualCredentialsPanel = BuildManualCredentialsPanel();
        _azureSetupPanel = BuildAzureSetupPanel();
        RefreshCredentialVariant();

        var chrome = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 4,
            Margin = new Thickness(16, 12, 16, 8),
            Children =
            {
                ReadAloudTheme.BrandTitle("Read Aloud", 26),
                ReadAloudTheme.Muted("Scratch reader — listen now, or write an MP3."),
                providerRow,
                actions,
                _status,
                _deviceUsage,
                ReadAloudTheme.Muted("Azure usage — last 30 days", 12),
                _usageStatus,
                _usageRefreshButton,
                ReadAloudTheme.Muted("Diagnostics", 12),
                _diagnosticsView,
                _azureSetupPanel,
            },
        };

        SetDock(chrome, Dock.Top);
        Children.Add(chrome);
        Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 280,
            Margin = new Thickness(16, 0, 16, 16),
            Padding = new Thickness(12),
            Background = GraphicalProfile.SurfaceBrush,
            CornerRadius = new CornerRadius(GraphicalProfileColors.CardRadius),
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            Child = _textBox,
        });

        _speech.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshListen);
        _speech.PlaybackStarted += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_speech.IsSpeaking)
                SetStatus("Playing…");
        });
        DetachedFromVisualTree += (_, _) =>
        {
            _speech.Stop();
            ReleaseWake();
        };

        RefreshProviderUi();
        RefreshListen();
        _ = InitializeSpeechAsync();
    }

    string DocumentText => _textBox.Text ?? string.Empty;

    bool HasDocument => !string.IsNullOrWhiteSpace(DocumentText);

    async Task InitializeSpeechAsync()
    {
        try
        {
            await _speech.InitializeAsync();
            if (_speech.CanCreateMp3)
                await _speech.UseAzureSpeechAsync();
            Dispatcher.UIThread.Post(() =>
            {
                RefreshManualCredentialFields();
                RefreshProviderUi();
                RefreshListen();
            });
            if (_speech.CanCreateMp3)
                await RefreshVoiceListAsync();
            Dispatcher.UIThread.Post(() =>
            {
                RefreshDeviceUsage();
                RefreshDiagnosticsView();
                if (CanRefreshAzureUsage())
                    _ = RefreshUsageAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud speech setup load failed.");
            Dispatcher.UIThread.Post(() =>
            {
                SetStatus("Azure Speech setup needs attention.");
                RefreshProviderUi();
                RefreshListen();
            });
        }
    }

    Border BuildAzureSetupPanel()
    {
        var children = new List<Control>
        {
            ReadAloudTheme.Muted(
                "Azure Speech credentials are used only for Azure playback and MP3 export.",
                13),
            _credentialStatus,
        };

        if (_resourcePicker is not null)
        {
            children.Add(ReadAloudTheme.Muted("Credential variant", 12));
            children.Add(_credentialVariantPicker);
            children.Add(_automaticCredentialsPanel);
        }

        children.Add(_manualCredentialsPanel);
        var content = new StackPanel
        {
            Spacing = 5,
        };
        foreach (var child in children)
            content.Children.Add(child);

        return new Border
        {
            IsVisible = false,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(10),
            Background = GraphicalProfile.SurfaceBrush,
            CornerRadius = new CornerRadius(4),
            BorderBrush = GraphicalProfile.AccentBrush,
            BorderThickness = new Thickness(1),
            Child = content,
        };
    }

    StackPanel BuildAutomaticCredentialsPanel()
    {
        var actions = new WrapPanel();
        foreach (var button in new[] { _signInButton, _signOutButton })
        {
            button.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(button);
        }

        return new StackPanel
        {
            Spacing = 5,
            IsVisible = false,
            Children =
            {
                ReadAloudTheme.Muted(
                    "Automatic uses Microsoft sign-in, then filters your subscriptions, resource groups, and compatible Speech services. No key file is used.",
                    13),
                actions,
                _resourceStatus,
                ReadAloudTheme.Muted("Azure subscription", 12),
                _subscriptionPicker,
                ReadAloudTheme.Muted("Resource group", 12),
                _resourceGroupPicker,
                ReadAloudTheme.Muted("Speech service", 12),
                _speechResourcePicker,
            },
        };
    }

    StackPanel BuildManualCredentialsPanel()
    {
        var test = ReadAloudTheme.Button("Test saved connection", ReadAloudButtonKind.Secondary);
        test.Click += async (_, _) => await TestAzureAsync();
        var remove = ReadAloudTheme.Button("Remove Azure setup", ReadAloudButtonKind.Danger);
        remove.Click += async (_, _) => await RemoveAzureSetupAsync();
        var actions = new WrapPanel();
        foreach (var button in new[] { _saveCredentialsButton, _importButton, test, remove })
        {
            button.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(button);
        }

        return new StackPanel
        {
            IsVisible = false,
            Spacing = 5,
            Children =
            {
                ReadAloudTheme.Muted(
                    "Enter the Speech endpoint and subscription key, or import a versioned JSON file. The key is stored only in platform secure storage.",
                    13),
                ReadAloudTheme.Muted(
                    "JSON layout: schema, version, authentication, endpoint, subscriptionKey. Optional: voiceName and locale.",
                    13),
                ReadAloudTheme.Muted(
                    "Azure Monitor usage requires Automatic — Microsoft sign-in. A manual key can synthesize but cannot read management metrics.",
                    13),
                ReadAloudTheme.Muted("Endpoint", 12),
                _endpointBox,
                ReadAloudTheme.Muted("Subscription key", 12),
                _keyBox,
                actions,
            },
        };
    }

    void ToggleAzureSetup()
    {
        _azureSetupPanel.IsVisible = !_azureSetupPanel.IsVisible;
        if (!_azureSetupPanel.IsVisible)
            return;

        var setup = _speech.AzureConfiguration;
        if (_resourcePicker is not null && setup is not null)
        {
            _credentialVariantPicker.SelectedIndex =
                setup.EffectiveCredentialSource == AzureSpeechCredentialSource.Automatic ? 0 : 1;
        }

        RefreshManualCredentialFields();
        RefreshCredentialVariant();
        SetStatus(_resourcePicker is not null &&
                  _credentialVariantPicker.SelectedIndex == 0
            ? "Automatic Azure sign-in: choose a Speech resource."
            : "Manual Azure credentials: enter the endpoint and key, or import a JSON file.");
    }

    void RefreshCredentialVariant()
    {
        var automatic = _resourcePicker is not null &&
                        _credentialVariantPicker.SelectedIndex == 0;
        _automaticCredentialsPanel.IsVisible = automatic;
        _manualCredentialsPanel.IsVisible = !automatic;

        var setup = _speech.AzureConfiguration;
        _credentialStatus.Text = setup is null
            ? "Current credentials: not configured."
            : $"Current credentials: {DescribeCredentialSource(setup)}.";

        var usageReady = CanRefreshAzureUsage();
        _usageRefreshButton.IsEnabled = usageReady && !_usageRefresh;
        if (!usageReady)
        {
            _azureUsageLoaded = false;
            _usageStatus.Text = _resourcePicker is null
                ? "Azure Monitor totals need Android Microsoft sign-in. Characters sent, calls, and failures on this device are listed above."
                : automatic
                    ? "Sign in and select an Azure Speech service to load the last 30 days."
                    : "Azure Monitor usage requires Automatic — Microsoft sign-in. A subscription key can synthesize but cannot read management metrics.";
        }
        else if (!_azureUsageLoaded && !_usageRefresh)
        {
            _usageStatus.Text = "Tap Refresh usage to load the last 30 days.";
        }
    }

    bool CanRefreshAzureUsage()
    {
        var setup = _speech.AzureConfiguration;
        return _resourcePicker is not null &&
               _credentialVariantPicker.SelectedIndex == 0 &&
               setup is { EffectiveCredentialSource: AzureSpeechCredentialSource.Automatic };
    }

    void RefreshDeviceUsage() => _deviceUsage.Text = _speech.DeviceUsageSummary;

    void RefreshDiagnosticsView()
    {
        var summary = _journal.ReadRecentSummary();
        _diagnosticsView.Text = string.IsNullOrWhiteSpace(summary)
            ? "No diagnostic events yet."
            : summary;
    }

    async Task RefreshUsageAsync()
    {
        if (_resourcePicker is null)
        {
            _usageStatus.Text =
                "Azure Monitor usage is available on Android automatic sign-in.";
            return;
        }

        var setup = _speech.AzureConfiguration;
        if (setup is null ||
            setup.EffectiveCredentialSource != AzureSpeechCredentialSource.Automatic)
        {
            _usageStatus.Text =
                "Switch to Automatic — Microsoft sign-in to view Azure Monitor usage.";
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
            _usageStatus.Text = $"Azure usage unavailable: {ex.Message}";
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

        _signInButton.IsEnabled = false;
        SetStatus("Opening Microsoft sign-in…");
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
                SetStatus("No enabled Azure subscriptions are available to this account.");
                return;
            }

            if (_subscriptions.Count == 1)
            {
                await LoadResourceGroupsAsync(_subscriptions[0]);
                return;
            }

            _resourceStatus.Text = "Choose the Azure subscription to search.";
            SetStatus("Choose an Azure subscription.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Azure sign-in cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure sign-in failed.");
            SetStatus($"Azure sign-in failed: {ex.Message}");
        }
        finally
        {
            _resourceRefresh = false;
            _signInButton.IsEnabled = true;
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
        SetStatus("Reading manual Azure credentials…");
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var setup = await AzureSpeechCredentialFile.ReadAsync(
                stream,
                _speech.Voice.Voice,
                LocaleFromVoice(_speech.Voice.Voice));
            await _speech.ConfigureAzureAsync(setup);
            _endpointBox.Text = setup.Endpoint.AbsoluteUri;
            _keyBox.Text = string.Empty;
            if (_resourcePicker is not null)
                _credentialVariantPicker.SelectedIndex = 1;
            RefreshCredentialVariant();
            await RefreshVoiceListAsync();
            SetStatus($"Manual Azure Speech credentials imported ({_voices.Count:N0} voices available).");
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud manual Azure Speech credential import failed.");
            SetStatus($"Credential file import failed: {ex.Message}");
        }
        finally
        {
            _importButton.IsEnabled = true;
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
            SetStatus($"Finding Speech resources in {subscription.DisplayName}…");
            _resourceGroups = await _resourcePicker.GetSpeechResourceGroupsAsync(subscription.Id);
            _resourceRefresh = true;
            _resourceGroupPicker.ItemsSource =
                _resourceGroups
                    .Select(group => $"{group.Name} ({group.SpeechResourceCount:N0})")
                    .ToArray();
            _resourceGroupPicker.SelectedIndex = _resourceGroups.Count == 1 ? 0 : -1;
            _resourceGroupPicker.IsVisible = _resourceGroups.Count > 1;
            _resourceGroupPicker.IsEnabled = _resourceGroups.Count > 0;
            ResetSpeechResourcePickerCore();
            _resourceRefresh = false;

            if (_resourceGroups.Count == 0)
            {
                SetStatus("No compatible Azure Speech resource was found.");
                _resourceStatus.Text =
                    "No resource group contains a compatible Speech service.";
                return;
            }

            if (_resourceGroups.Count == 1)
            {
                await LoadSpeechResourcesAsync(subscription, _resourceGroups[0]);
                return;
            }

            _resourceStatus.Text = "Choose the resource group to search.";
            SetStatus("Choose an Azure resource group.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Azure resource discovery cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure resource-group discovery failed.");
            SetStatus($"Resource discovery failed: {ex.Message}");
        }
        finally
        {
            _resourceRefresh = false;
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
            SetStatus($"Finding Speech services in {resourceGroup.Name}…");
            _speechResources = await _resourcePicker.GetSpeechResourcesAsync(
                subscription.Id,
                resourceGroup.Name);
            _resourceRefresh = true;
            _speechResourcePicker.ItemsSource =
                _speechResources
                    .Select(resource => $"{resource.Name} ({resource.Location})")
                    .ToArray();
            _speechResourcePicker.SelectedIndex = _speechResources.Count == 1 ? 0 : -1;
            _speechResourcePicker.IsVisible = _speechResources.Count > 1;
            _speechResourcePicker.IsEnabled = _speechResources.Count > 0;
            _resourceRefresh = false;

            if (_speechResources.Count == 0)
            {
                SetStatus("No compatible Speech service was found in that group.");
                return;
            }

            if (_speechResources.Count == 1)
            {
                await ConfigureAzureResourceAsync(_speechResources[0]);
                return;
            }

            _resourceStatus.Text = "Choose the Speech service to use.";
            SetStatus("Choose an Azure Speech service.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Azure resource discovery cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure Speech discovery failed.");
            SetStatus($"Speech discovery failed: {ex.Message}");
        }
        finally
        {
            _resourceRefresh = false;
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
            SetStatus($"Connecting to {resource.Name}…");
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
            SetStatus($"Connected to {resource.Name} ({_voices.Count:N0} voices available).");
            RefreshProviderUi();
            RefreshListen();
            _usageStatus.Text = "Tap Refresh usage to load the last 30 days.";
            await RefreshUsageAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Azure connection cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure Speech resource connection failed.");
            SetStatus($"Azure connection failed: {ex.Message}");
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
            _keyBox.Text = string.Empty;
            RefreshManualCredentialFields();
            ResetVoicePickerCore();
            RefreshCredentialVariant();
            SetStatus("Signed out. Azure Speech setup is required to listen.");
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure sign-out failed.");
            SetStatus($"Azure sign-out failed: {ex.Message}");
        }
    }

    void ResetResourcePickersCore()
    {
        _resourceGroupPicker.ItemsSource = Array.Empty<string>();
        _resourceGroupPicker.SelectedIndex = -1;
        _resourceGroupPicker.IsVisible = false;
        _resourceGroupPicker.IsEnabled = false;
        ResetSpeechResourcePickerCore();
    }

    void ResetSpeechResourcePickerCore()
    {
        _speechResourcePicker.ItemsSource = Array.Empty<string>();
        _speechResourcePicker.SelectedIndex = -1;
        _speechResourcePicker.IsVisible = false;
        _speechResourcePicker.IsEnabled = false;
    }

    async Task SaveManualCredentialsAsync()
    {
        _saveCredentialsButton.IsEnabled = false;
        SetStatus("Saving manual Azure credentials…");
        try
        {
            var setup = AzureSpeechCredentialFile.FromManualEntry(
                _endpointBox.Text ?? string.Empty,
                _keyBox.Text,
                _speech.Voice.Voice,
                LocaleFromVoice(_speech.Voice.Voice));
            await _speech.ConfigureAzureAsync(setup);
            _endpointBox.Text = setup.Endpoint.AbsoluteUri;
            _keyBox.Text = string.Empty;
            if (_resourcePicker is not null)
                _credentialVariantPicker.SelectedIndex = 1;
            RefreshCredentialVariant();
            await RefreshVoiceListAsync();
            SetStatus($"Manual Azure Speech credentials saved ({_voices.Count:N0} voices available).");
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud manual Azure Speech credential save failed.");
            SetStatus($"Credential save failed: {ex.Message}");
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
            SetStatus($"Voice set to {item.Display}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud voice selection failed.");
            SetStatus($"Voice selection failed: {ex.Message}");
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
        if (setup is null)
        {
            if (string.IsNullOrWhiteSpace(_endpointBox.Text))
                _endpointBox.Text = string.Empty;
            return;
        }

        _endpointBox.Text = setup.Endpoint.AbsoluteUri;
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
            SetStatus("Testing saved Azure Speech connection…");
            await _speech.TestAzureAsync();
            await RefreshVoiceListAsync();
            SetStatus($"Azure Speech connected ({_voices.Count:N0} voices available).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure connection test failed.");
            SetStatus($"Azure test failed: {ex.Message}");
        }
    }

    async Task RemoveAzureSetupAsync()
    {
        try
        {
            await _speech.RemoveAzureAsync();
            _azureSetupPanel.IsVisible = false;
            _keyBox.Text = string.Empty;
            RefreshManualCredentialFields();
            ResetVoicePickerCore();
            RefreshCredentialVariant();
            RefreshProviderUi();
            RefreshListen();
            SetStatus("Azure setup removed. Enter credentials or sign in to listen.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure setup removal failed.");
            SetStatus($"Could not remove Azure setup: {ex.Message}");
        }
    }

    void RefreshProviderUi()
    {
        _configureButton.Content = _speech.CanCreateMp3 ? "Azure settings" : "Azure setup";
        RefreshCredentialVariant();
    }

    static string DescribeCredentialSource(AzureSpeechSetup setup) =>
        setup.EffectiveCredentialSource == AzureSpeechCredentialSource.Automatic
            ? "Automatic — Microsoft sign-in"
            : "Manual — endpoint and subscription key";

    void Clear()
    {
        _speech.Stop();
        _textBox.Text = string.Empty;
        SetStatus(_speech.CanCreateMp3
            ? "Azure Speech selected. Requests use your resource and quota."
            : "Azure Speech setup is required to listen.");
        RefreshListen();
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
        _textBox.Text = await reader.ReadToEndAsync();
        var label = files[0].Name;
        if (string.IsNullOrWhiteSpace(label))
            label = files[0].TryGetLocalPath() ?? "Opened file";
        SetStatus($"Opened {label}");
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
            SetStatus("Clipboard is empty.");
            return;
        }

        _textBox.Text = text;
        SetStatus("Pasted clipboard.");
        RefreshListen();
    }

    async Task OnListenClickAsync()
    {
        if (_speech.IsSpeaking)
        {
            _speech.Stop();
            ReleaseWake();
            SetStatus("Stopped.");
            RefreshListen();
            return;
        }

        if (_listenPending)
            return;

        if (!_speech.CanCreateMp3)
        {
            _azureSetupPanel.IsVisible = true;
            RefreshCredentialVariant();
            SetStatus(
                _resourcePicker is null
                    ? "Enter Azure Speech credentials or import a JSON file before listening."
                    : "Sign in or enter Azure Speech credentials before listening.");
            return;
        }

        var text = DocumentText;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Paste or type something first.");
            return;
        }

        _wake = _wakeLock.Acquire("read-aloud-listen");
        _listenPending = true;
        SetStatus("Synthesizing…");
        RefreshListen();
        try
        {
            await _speech.SpeakAsync(text);
            if (!_speech.IsSpeaking)
                SetStatus(_speech.HasCachedAudio(text) ? "Done. Audio is cached." : "Done.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud listening failed.");
            SetStatus($"Listen failed: {ex.Message}");
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
            SetStatus("Paste or type something first.");
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
            _azureSetupPanel.IsVisible = true;
            SetStatus(
                _resourcePicker is null
                    ? "MP3 export needs typed or imported Azure credentials."
                    : "MP3 export needs automatic sign-in or typed/imported Azure credentials.");
            RefreshCredentialVariant();
            return;
        }

        _saveButton.IsEnabled = false;
        SetStatus("Writing MP3…");
        try
        {
            var mp3 = await _speech.SynthesizeDocumentMp3Async(text);
            if (mp3.Length == 0)
            {
                SetStatus("Nothing to save — Azure Speech returned empty audio.");
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(mp3);
            SetStatus($"Saved {file.Name} ({mp3.Length:N0} bytes).");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Save cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud MP3 export failed.");
            SetStatus($"Save failed: {ex.Message}");
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
        _textBox.IsReadOnly = speaking;
        _textBox.Opacity = speaking ? 0.62 : 1;
        _listenButton.Content = speaking ? "Stop" : (_speech.HasCachedAudio(DocumentText) ? "Listen ✓" : "Listen");
        ReadAloudTheme.StyleButton(_listenButton, ReadAloudButtonKind.Primary);
        _saveButton.IsEnabled = !speaking && _speech.CanCreateMp3;
        RefreshDeviceUsage();
        RefreshProviderUi();
    }

    async Task ShareDiagnosticsAsync()
    {
        try
        {
            RefreshDiagnosticsView();
            await _diagnosticShare.ShareLatestAsync();
            SetStatus("Diagnostics are on screen. The latest file is ready to share.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud diagnostics export failed.");
            SetStatus($"Diagnostics failed: {ex.Message}");
        }
    }

    void SetStatus(string text) => _status.Text = text;

    void ReleaseWake()
    {
        _wake?.Dispose();
        _wake = null;
    }
}
