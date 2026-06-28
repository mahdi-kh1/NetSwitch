using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NetSwitch.Interop;
using NetSwitch.Models;
using NetSwitch.Services;

namespace NetSwitch;

public partial class MainWindow : Window
{
    private readonly NetworkService _net;
    private readonly SwitchController _controller;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly AutoSwitchMonitor _autoMonitor;
    private readonly DispatcherTimer _liveDebounce;

    private bool _suppressAutoToggle;
    private bool _busy;
    private bool _liveSubscribed;

    public MainWindow(
        NetworkService net,
        SwitchController controller,
        SettingsService settingsService,
        AppSettings settings)
    {
        InitializeComponent();
        _net = net;
        _controller = controller;
        _settingsService = settingsService;
        _settings = settings;

        _autoMonitor = new AutoSwitchMonitor(controller, settings);
        _autoMonitor.Switched += OnAutoSwitched;
        if (_settings.AutoSwitch)
            _autoMonitor.Start();

        // Coalesce bursts of network-change events into a single refresh.
        _liveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _liveDebounce.Tick += async (_, _) => { _liveDebounce.Stop(); await RefreshAsync(); };

        // Live-update only while the panel is visible (like Control Panel).
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) SubscribeLive();
            else UnsubscribeLive();
        };
    }

    private void SubscribeLive()
    {
        if (_liveSubscribed) return;
        _liveSubscribed = true;
        NetworkChange.NetworkAddressChanged += OnNetworkLive;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkLive;
    }

    private void UnsubscribeLive()
    {
        if (!_liveSubscribed) return;
        _liveSubscribed = false;
        NetworkChange.NetworkAddressChanged -= OnNetworkLive;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkLive;
        _liveDebounce.Stop();
    }

    private void OnNetworkLive(object? sender, EventArgs e)
    {
        // Marshal to UI thread and debounce.
        Dispatcher.BeginInvoke(() => { _liveDebounce.Stop(); _liveDebounce.Start(); });
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AcrylicHelper.EnableAcrylic(this, 0x73221E1C);
    }

    public void PositionNearTray()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 12;
        Top = wa.Bottom - Height - 12;
    }

    public void ShowPanel()
    {
        PositionNearTray();
        Show();
        Activate();
        Topmost = true;
        _ = RefreshAsync();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        Hide();
    }

    /// <summary>Re-read adapter state and repaint the panel.</summary>
    public async Task RefreshAsync()
    {
        var snapshot = await Task.Run(() => _controller.GetSnapshot(_settings));
        var wifi = snapshot.Wifi;
        var eth = snapshot.Ethernet;
        var active = snapshot.Active;

        if (active is not null)
        {
            ActiveName.Text = active.IsWireless ? "Wi-Fi" : "Ethernet";
            ActiveStatus.Text = active.Name;
        }
        else
        {
            ActiveName.Text = "Not connected";
            ActiveStatus.Text = "No active adapter";
        }

        WifiSub.Text = wifi?.StatusText ?? "Not found";
        EthSub.Text = eth?.StatusText ?? "Not found";

        SetTileActive(WifiTile, active is { IsWireless: true });
        SetTileActive(EthTile, active is { Kind: AdapterKind.Ethernet });

        _suppressAutoToggle = true;
        AutoToggle.IsChecked = _settings.AutoSwitch;
        _suppressAutoToggle = false;
    }

    private void SetTileActive(Button tile, bool active)
    {
        tile.Background = (System.Windows.Media.Brush)FindResource(active ? "GlassActiveBrush" : "GlassFillBrush");
        tile.BorderBrush = (System.Windows.Media.Brush)FindResource(active ? "GlassActiveBorderBrush" : "GlassBorderBrush");
    }

    private async void OnAutoSwitched(object? sender, EventArgs e)
    {
        await Dispatcher.InvokeAsync(async () => await RefreshAsync());
    }

    private async Task DoSwitchAsync(NetworkTarget target)
    {
        if (_busy) return;
        _busy = true;
        WifiTile.IsEnabled = EthTile.IsEnabled = false;
        try
        {
            await _controller.SwitchToAsync(target, _settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "NetSwitch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            WifiTile.IsEnabled = EthTile.IsEnabled = true;
            _busy = false;
        }

        // Adapters take a moment to come up — refresh a few times so the UI
        // catches the new state quickly (live events also keep it in sync).
        await RefreshSoonAsync();
    }

    private async Task RefreshSoonAsync()
    {
        await RefreshAsync();
        await Task.Delay(500);
        await RefreshAsync();
        await Task.Delay(800);
        await RefreshAsync();
    }

    public void OpenSettings()
    {
        var win = new SettingsWindow(_net, _settings, _settingsService);
        win.ShowDialog();

        // Settings may have toggled auto mode or changed adapters.
        if (_settings.AutoSwitch)
            _autoMonitor.Start();
        else
            _autoMonitor.Stop();
        _ = RefreshAsync();
    }

    // ---------------- UI events ----------------
    private void Header_DragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void SwitchWifi_Click(object sender, RoutedEventArgs e) =>
        await DoSwitchAsync(NetworkTarget.Wifi);

    private async void SwitchEthernet_Click(object sender, RoutedEventArgs e) =>
        await DoSwitchAsync(NetworkTarget.Ethernet);

    private void Auto_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressAutoToggle) return;
        _settings.AutoSwitch = AutoToggle.IsChecked == true;
        _settingsService.Save(_settings);
        if (_settings.AutoSwitch)
            _autoMonitor.Start();
        else
            _autoMonitor.Stop();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void Quit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
