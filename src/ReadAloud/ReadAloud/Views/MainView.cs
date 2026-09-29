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
using ReadAloud.Services;
using ReadAloud.Ui;

namespace ReadAloud.Views;

/// <summary>Paste or open text, listen with device voice or Azure Speech, or save an MP3.</summary>
public sealed class MainView : DockPanel
{
    readonly SpeechService _speech;
    readonly IScreenWakeLock _wakeLock;
    readonly IDiagnosticShare _diagnosticShare;
    readonly ILogger<MainView> _logger;
    readonly TextBox _textBox;
    readonly Button _listenButton;
    readonly Button _saveButton;
    readonly TextBlock _status;
    readonly ComboBox _providerPicker;
    readonly Button _configureButton;
    readonly Border _azureSetupPanel;
    readonly TextBox _endpointBox;
    readonly TextBox _keyBox;
    readonly TextBox _clientIdBox;
    readonly TextBox _tenantIdBox;
    readonly ComboBox _authModePicker;
    readonly StackPanel _keyFields;
    readonly StackPanel _entraFields;
    readonly IAzureSpeechResourcePicker? _resourcePicker;
    readonly IAzureSpeechFallbackProvider? _fallbackProvider;
    readonly Button _signInButton;
    readonly Button _fallbackButton;
    readonly Button _signOutButton;
    readonly TextBlock _resourceStatus;
    readonly ComboBox _subscriptionPicker;
    readonly ComboBox _resourceGroupPicker;
    readonly ComboBox _speechResourcePicker;
    IReadOnlyList<AzureSubscriptionChoice> _subscriptions = [];
    IReadOnlyList<AzureSpeechResourceGroupChoice> _resourceGroups = [];
    IReadOnlyList<AzureSpeechResourceChoice> _speechResources = [];
    bool _providerRefresh;
    bool _providerReady;
    bool _resourceRefresh;
    IDisposable? _wake;

    public MainView(
        SpeechService speech,
        IScreenWakeLock wakeLock,
        IDiagnosticShare diagnosticShare,
        ILogger<MainView> logger,
        IAzureSpeechResourcePicker? resourcePicker = null,
        IAzureSpeechFallbackProvider? fallbackProvider = null)
    {
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _wakeLock = wakeLock ?? throw new ArgumentNullException(nameof(wakeLock));
        _diagnosticShare = diagnosticShare ?? throw new ArgumentNullException(nameof(diagnosticShare));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resourcePicker = resourcePicker;
        _fallbackProvider = fallbackProvider;

        LastChildFill = true;
        Background = ReadAloudPalette.WindowBrush;

        _textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Paste or type anything to hear…",
            FontFamily = ReadAloudPalette.BodyFont,
            FontSize = 16,
            Foreground = ReadAloudPalette.BodyBrush,
            Background = ReadAloudPalette.PanelRaisedBrush,
            CaretBrush = ReadAloudPalette.AccentBrush,
        };

        var openBtn = ReadAloudTheme.Button("Open file…", ReadAloudButtonKind.Secondary);
        openBtn.Click += async (_, _) => await OpenFileAsync();
        var pasteBtn = ReadAloudTheme.Button("Paste", ReadAloudButtonKind.Secondary);
        pasteBtn.Click += async (_, _) => await PasteClipboardAsync();
        _listenButton = ReadAloudTheme.Button("Listen", ReadAloudButtonKind.Primary);
        _listenButton.Click += async (_, _) => await OnListenClickAsync();
        _saveButton = ReadAloudTheme.Button("Save MP3", ReadAloudButtonKind.Secondary);
        _saveButton.Click += async (_, _) => await SaveMp3Async();
        _providerPicker = new ComboBox
        {
            ItemsSource = new[] { "Device voice", "Azure Speech" },
            SelectedIndex = 0,
            MinWidth = 150,
            FontFamily = ReadAloudPalette.BodyFont,
        };
        _providerPicker.SelectionChanged += async (_, _) => await OnProviderChangedAsync();
        _configureButton = ReadAloudTheme.Button("Azure setup", ReadAloudButtonKind.Secondary);
        _configureButton.Click += (_, _) => ToggleAzureSetup();
        var diagnosticsBtn = ReadAloudTheme.Button(_diagnosticShare.ActionLabel, ReadAloudButtonKind.Quiet);
        diagnosticsBtn.Click += async (_, _) => await ShareDiagnosticsAsync();
        var clearBtn = ReadAloudTheme.Button("Clear", ReadAloudButtonKind.Quiet);
        clearBtn.Click += (_, _) => Clear();

        var actions = new WrapPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
        };
        foreach (var btn in new[] { openBtn, pasteBtn, _listenButton, _saveButton, diagnosticsBtn, clearBtn })
        {
            btn.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(btn);
        }

        var providerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                ReadAloudTheme.Muted("Voice source:", 14),
                _providerPicker,
                _configureButton,
            },
        };

        _status = ReadAloudTheme.Muted("Device voice works offline. Azure Speech adds MP3 export.");

        _endpointBox = new TextBox
        {
            PlaceholderText = "https://your-resource.cognitiveservices.azure.com/",
            FontFamily = ReadAloudPalette.BodyFont,
            Foreground = ReadAloudPalette.BodyBrush,
            Background = ReadAloudPalette.PanelRaisedBrush,
        };
        _authModePicker = new ComboBox
        {
            ItemsSource = new[] { "Subscription key", "Microsoft Entra sign-in" },
            SelectedIndex = 0,
            FontFamily = ReadAloudPalette.BodyFont,
        };
        _authModePicker.SelectionChanged += (_, _) => RefreshAuthFields();
        _keyBox = new TextBox
        {
            PlaceholderText = "Speech resource key",
            PasswordChar = '•',
            FontFamily = ReadAloudPalette.BodyFont,
            Foreground = ReadAloudPalette.BodyBrush,
            Background = ReadAloudPalette.PanelRaisedBrush,
        };
        _clientIdBox = new TextBox
        {
            PlaceholderText = "Registered Entra application client id",
            FontFamily = ReadAloudPalette.BodyFont,
            Foreground = ReadAloudPalette.BodyBrush,
            Background = ReadAloudPalette.PanelRaisedBrush,
        };
        _tenantIdBox = new TextBox
        {
            PlaceholderText = "Tenant id (optional)",
            FontFamily = ReadAloudPalette.BodyFont,
            Foreground = ReadAloudPalette.BodyBrush,
            Background = ReadAloudPalette.PanelRaisedBrush,
        };
        _keyFields = new StackPanel { Spacing = 4, Children = { _keyBox } };
        _entraFields = new StackPanel
        {
            Spacing = 4,
            Children = { _clientIdBox, _tenantIdBox },
            IsVisible = false,
        };
        _signInButton = ReadAloudTheme.Button(
            "Sign in with Microsoft",
            ReadAloudButtonKind.Primary);
        _signInButton.Click += async (_, _) => await SignInAzureAsync();
        _fallbackButton = ReadAloudTheme.Button(
            "Import credentials",
            ReadAloudButtonKind.Secondary);
        _fallbackButton.Click += async (_, _) => await ImportLocalAzureCredentialsAsync();
        _fallbackButton.IsVisible = _fallbackProvider?.IsAvailable == true;
        _signOutButton = ReadAloudTheme.Button("Sign out", ReadAloudButtonKind.Quiet);
        _signOutButton.Click += async (_, _) => await SignOutAzureAsync();
        _signOutButton.IsVisible = false;
        _resourceStatus = ReadAloudTheme.Muted(
            "Sign in to choose an Azure subscription.",
            13);
        _subscriptionPicker = new ComboBox
        {
            MinWidth = 240,
            IsEnabled = false,
            FontFamily = ReadAloudPalette.BodyFont,
        };
        _subscriptionPicker.SelectionChanged += async (_, _) =>
            await OnSubscriptionSelectedAsync();
        _resourceGroupPicker = new ComboBox
        {
            MinWidth = 240,
            IsVisible = false,
            IsEnabled = false,
            FontFamily = ReadAloudPalette.BodyFont,
        };
        _resourceGroupPicker.SelectionChanged += async (_, _) =>
            await OnResourceGroupSelectedAsync();
        _speechResourcePicker = new ComboBox
        {
            MinWidth = 240,
            IsVisible = false,
            IsEnabled = false,
            FontFamily = ReadAloudPalette.BodyFont,
        };
        _speechResourcePicker.SelectionChanged += async (_, _) =>
            await OnSpeechResourceSelectedAsync();
        _azureSetupPanel = BuildAzureSetupPanel();
        RefreshAuthFields();

        var chrome = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(16, 12, 16, 8),
            Children =
            {
                ReadAloudTheme.BrandTitle("Read Aloud", 26),
                ReadAloudTheme.Muted("Scratch reader — listen now, or write an MP3."),
                providerRow,
                actions,
                _status,
                _azureSetupPanel,
            },
        };

        SetDock(chrome, Dock.Top);
        Children.Add(chrome);
        Children.Add(new Border
        {
            Margin = new Thickness(16, 0, 16, 16),
            Padding = new Thickness(12),
            Background = ReadAloudPalette.PanelBrush,
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 47, 223, 255)),
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
            Dispatcher.UIThread.Post(() =>
            {
                _providerReady = true;
                RefreshProviderUi();
                RefreshListen();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud speech setup load failed.");
            Dispatcher.UIThread.Post(() =>
            {
                _providerReady = true;
                SetStatus("Device voice is ready; saved Azure setup needs attention.");
                RefreshProviderUi();
                RefreshListen();
            });
        }
    }

    Border BuildAzureSetupPanel() =>
        _resourcePicker is not null
            ? BuildAzureResourcePickerPanel()
            : BuildManualAzureSetupPanel();

    Border BuildAzureResourcePickerPanel() =>
        new Border
        {
            IsVisible = false,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(10),
            Background = ReadAloudPalette.PanelBrush,
            CornerRadius = new CornerRadius(4),
            BorderBrush = ReadAloudPalette.AccentBrush,
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    ReadAloudTheme.Muted(
                        "Sign in with Microsoft. Read Aloud will show only subscriptions and Speech resources you can access.",
                        13),
                    new WrapPanel
                    {
                        Children = { _signInButton, _fallbackButton, _signOutButton },
                    },
                    _resourceStatus,
                    ReadAloudTheme.Muted("Azure subscription", 12),
                    _subscriptionPicker,
                    ReadAloudTheme.Muted("Resource group", 12),
                    _resourceGroupPicker,
                    ReadAloudTheme.Muted("Speech service", 12),
                    _speechResourcePicker,
                },
            },
        };

    Border BuildManualAzureSetupPanel()
    {
        var save = ReadAloudTheme.Button("Save and test", ReadAloudButtonKind.Primary);
        save.Click += async (_, _) => await SaveAzureSetupAsync();
        var test = ReadAloudTheme.Button("Test saved connection", ReadAloudButtonKind.Secondary);
        test.Click += async (_, _) => await TestAzureAsync();
        var remove = ReadAloudTheme.Button("Remove Azure setup", ReadAloudButtonKind.Danger);
        remove.Click += async (_, _) => await RemoveAzureSetupAsync();

        return new Border
        {
            IsVisible = false,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(10),
            Background = ReadAloudPalette.PanelBrush,
            CornerRadius = new CornerRadius(4),
            BorderBrush = ReadAloudPalette.AccentBrush,
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    ReadAloudTheme.Muted(
                        "Read Aloud signs in to your Azure Speech resource. The endpoint and credential are stored in platform secure storage.",
                        13),
                    ReadAloudTheme.Muted("Resource endpoint", 12),
                    _endpointBox,
                    ReadAloudTheme.Muted("Authentication", 12),
                    _authModePicker,
                    _keyFields,
                    _entraFields,
                    new WrapPanel
                    {
                        Children = { save, test, remove },
                    },
                },
            },
        };
    }

    void ToggleAzureSetup()
    {
        _azureSetupPanel.IsVisible = !_azureSetupPanel.IsVisible;
        if (!_azureSetupPanel.IsVisible)
            return;

        if (_resourcePicker is not null)
        {
            SetStatus(
                _subscriptions.Count == 0
                    ? "Sign in to choose an Azure Speech resource."
                    : "Choose an Azure Speech resource.");
            return;
        }

        if (_azureSetupPanel.IsVisible)
        {
            var setup = _speech.AzureConfiguration;
            if (setup is not null)
            {
                _endpointBox.Text = setup.Endpoint.ToString();
                _authModePicker.SelectedIndex =
                    setup.AuthenticationMode == AzureSpeechAuthenticationMode.ApiKey ? 0 : 1;
                _clientIdBox.Text = setup.ClientId ?? string.Empty;
                _tenantIdBox.Text = setup.TenantId ?? string.Empty;
            }
            else
            {
                _endpointBox.Text = SpeechService.DefaultAzureEndpoint.ToString();
                _authModePicker.SelectedIndex = 1;
                _clientIdBox.Text = SpeechService.DefaultAzureClientId;
                _tenantIdBox.Text = SpeechService.DefaultAzureTenantId;
            }

            RefreshAuthFields();
        }
    }

    void RefreshAuthFields()
    {
        if (_keyFields is null || _entraFields is null)
            return;
        var entra = _authModePicker.SelectedIndex == 1;
        _keyFields.IsVisible = !entra;
        _entraFields.IsVisible = entra;
    }

    async Task OnProviderChangedAsync()
    {
        if (_providerRefresh || !_providerReady)
            return;

        if (_providerPicker.SelectedIndex == 0)
        {
            await _speech.UseDeviceVoiceAsync();
            SetStatus("Device voice selected. It works without network access.");
            RefreshListen();
            return;
        }

        if (!_speech.CanCreateMp3)
        {
            _providerRefresh = true;
            _providerPicker.SelectedIndex = 0;
            _providerRefresh = false;
            _azureSetupPanel.IsVisible = true;
            SetStatus(
                _resourcePicker is null
                    ? "Add your Azure Speech resource before selecting Azure playback."
                    : "Sign in to choose an Azure Speech resource.");
            if (_resourcePicker is null)
                RefreshAuthFields();
            return;
        }

        await _speech.UseAzureSpeechAsync();
        SetStatus("Azure Speech selected. Requests use your resource and quota.");
        RefreshListen();
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

    async Task ImportLocalAzureCredentialsAsync()
    {
        if (_fallbackProvider is null)
            return;

        _fallbackButton.IsEnabled = false;
        SetStatus("Importing local Azure Speech credentials…");
        try
        {
            var setup = await _fallbackProvider.LoadAsync();
            await _speech.ConfigureAzureAsync(setup);
            var voices = await _speech.TestAzureAsync();
            _resourceStatus.Text = "Using imported local developer credentials.";
            SetStatus($"Azure Speech credentials imported ({voices.Count:N0} voices available).");
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud local Azure Speech credential import failed.");
            SetStatus($"Credential import failed: {ex.Message}");
        }
        finally
        {
            _fallbackButton.IsEnabled = _fallbackProvider.IsAvailable;
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
                AuthenticationMode = AzureSpeechAuthenticationMode.MicrosoftEntra,
                ClientId = resource.ClientId,
                TenantId = resource.TenantId,
                VoiceName = _speech.Voice.Voice,
                Locale = "en-US",
            });
            var voices = await _speech.TestAzureAsync();
            _azureSetupPanel.IsVisible = false;
            SetStatus($"Connected to {resource.Name} ({voices.Count:N0} voices available).");
            RefreshProviderUi();
            RefreshListen();
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
            SetStatus("Signed out. Device voice remains available.");
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

    async Task SaveAzureSetupAsync()
    {
        if (!Uri.TryCreate(_endpointBox.Text?.Trim(), UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Enter an absolute HTTPS Azure Speech endpoint.");
            return;
        }

        var entra = _authModePicker.SelectedIndex == 1;
        var setup = new AzureSpeechSetup
        {
            Endpoint = endpoint,
            AuthenticationMode = entra
                ? AzureSpeechAuthenticationMode.MicrosoftEntra
                : AzureSpeechAuthenticationMode.ApiKey,
            ApiKey = entra ? null : _keyBox.Text,
            ClientId = entra ? _clientIdBox.Text?.Trim() : null,
            TenantId = entra ? _tenantIdBox.Text?.Trim() : null,
            VoiceName = _speech.Voice.Voice,
            Locale = "en-US",
        };

        try
        {
            SetStatus(entra
                ? "Saving Azure setup and opening Microsoft sign-in…"
                : "Saving Azure setup and testing the resource…");
            await _speech.ConfigureAzureAsync(setup);
            var voices = await _speech.TestAzureAsync();
            SetStatus($"Azure Speech connected ({voices.Count:N0} voices available).");
            RefreshProviderUi();
            RefreshListen();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure setup failed.");
            SetStatus($"Azure setup failed: {ex.Message}");
        }
    }

    async Task TestAzureAsync()
    {
        try
        {
            SetStatus("Testing saved Azure Speech connection…");
            var voices = await _speech.TestAzureAsync();
            SetStatus($"Azure Speech connected ({voices.Count:N0} voices available).");
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
            RefreshProviderUi();
            RefreshListen();
            SetStatus("Azure setup removed. Device voice remains available.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read Aloud Azure setup removal failed.");
            SetStatus($"Could not remove Azure setup: {ex.Message}");
        }
    }

    void RefreshProviderUi()
    {
        _providerRefresh = true;
        _providerPicker.SelectedIndex = _speech.Provider == SpeechProvider.AzureSpeech ? 1 : 0;
        _providerRefresh = false;
        _configureButton.Content = _speech.CanCreateMp3 ? "Azure settings" : "Azure setup";
    }

    void Clear()
    {
        _speech.Stop();
        _textBox.Text = string.Empty;
        SetStatus(_speech.Provider == SpeechProvider.DeviceVoice
            ? "Device voice works offline."
            : "Azure Speech selected. Requests use your resource and quota.");
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

        var text = DocumentText;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Paste or type something first.");
            return;
        }

        _wake = _wakeLock.Acquire("read-aloud-listen");
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
            ReleaseWake();
            RefreshListen();
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
            SetStatus("MP3 export needs Azure Speech setup; device voice does not create files.");
            RefreshAuthFields();
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
        }
    }

    void RefreshListen()
    {
        var speaking = _speech.IsSpeaking;
        _listenButton.Content = speaking ? "Stop" : (_speech.HasCachedAudio(DocumentText) ? "Listen ✓" : "Listen");
        ReadAloudTheme.StyleButton(_listenButton, ReadAloudButtonKind.Primary);
        _saveButton.IsEnabled = !speaking && _speech.CanCreateMp3;
        RefreshProviderUi();
    }

    async Task ShareDiagnosticsAsync()
    {
        try
        {
            await _diagnosticShare.ShareLatestAsync();
            SetStatus("Diagnostics are ready.");
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
