using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using NetSwitch.Models;
using NetSwitch.Services;

namespace NetSwitch;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private MainWindow? _panel;

    private readonly SettingsService _settingsService = new();
    private readonly NetworkService _network = new();
    private SwitchController _controller = null!;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _controller = new SwitchController(_network);
        _settings = _settingsService.Load();
        EnsureAdaptersConfigured();

        _panel = new MainWindow(_network, _controller, _settingsService, _settings);

        _tray = new TaskbarIcon
        {
            ToolTipText = "NetSwitch — network switcher",
            Icon = LoadTrayIcon()
        };
        _tray.TrayLeftMouseUp += (_, _) => TogglePanel();
        _tray.ContextMenu = BuildContextMenu();
    }

    /// <summary>If the user hasn't picked adapters yet, guess sensible defaults and persist.</summary>
    private void EnsureAdaptersConfigured()
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(_settings.WifiAdapterGuid))
        {
            _settings.WifiAdapterGuid = _network.GuessWifi()?.Guid;
            changed |= _settings.WifiAdapterGuid is not null;
        }
        if (string.IsNullOrWhiteSpace(_settings.EthernetAdapterGuid))
        {
            _settings.EthernetAdapterGuid = _network.GuessEthernet()?.Guid;
            changed |= _settings.EthernetAdapterGuid is not null;
        }
        if (changed)
            _settingsService.Save(_settings);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        return File.Exists(path)
            ? new System.Drawing.Icon(path)
            : System.Drawing.SystemIcons.Application;
    }

    private void TogglePanel()
    {
        if (_panel is null) return;
        if (_panel.IsVisible)
            _panel.Hide();
        else
            _panel.ShowPanel();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Open NetSwitch" };
        open.Click += (_, _) => _panel?.ShowPanel();

        var wifi = new MenuItem { Header = "Switch to Wi-Fi" };
        wifi.Click += async (_, _) => await SwitchAsync(NetworkTarget.Wifi);

        var eth = new MenuItem { Header = "Switch to Ethernet" };
        eth.Click += async (_, _) => await SwitchAsync(NetworkTarget.Ethernet);

        var settings = new MenuItem { Header = "Settings…" };
        settings.Click += (_, _) => _panel?.OpenSettings();

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => Shutdown();

        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(wifi);
        menu.Items.Add(eth);
        menu.Items.Add(new Separator());
        menu.Items.Add(settings);
        menu.Items.Add(quit);
        return menu;
    }

    private async Task SwitchAsync(NetworkTarget target)
    {
        try
        {
            await _controller.SwitchToAsync(target, _settings);
            await _panel!.RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "NetSwitch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
