using System.Windows;
using System.Windows.Input;
using NetSwitch.Interop;
using NetSwitch.Models;
using NetSwitch.Services;

namespace NetSwitch;

public partial class SettingsWindow : Window
{
    private readonly NetworkService _net;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startup = new();

    public SettingsWindow(NetworkService net, AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _net = net;
        _settings = settings;
        _settingsService = settingsService;

        LoadAdapters();
        DisableOppositeToggle.IsChecked = _settings.DisableOppositeOnSwitch;
        AutoToggle.IsChecked = _settings.AutoSwitch;
        StartupToggle.IsChecked = _startup.IsEnabled();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AcrylicHelper.EnableAcrylic(this, 0x8C221E1C);
    }

    private void LoadAdapters()
    {
        var adapters = _net.GetPhysicalAdapters();
        WifiCombo.ItemsSource = adapters;
        EthCombo.ItemsSource = adapters;

        WifiCombo.SelectedItem =
            adapters.FirstOrDefault(a => a.Guid == _settings.WifiAdapterGuid)
            ?? adapters.FirstOrDefault(a => a.IsWireless);
        EthCombo.SelectedItem =
            adapters.FirstOrDefault(a => a.Guid == _settings.EthernetAdapterGuid)
            ?? adapters.FirstOrDefault(a => a.Kind == AdapterKind.Ethernet);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadAdapters();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.WifiAdapterGuid = (WifiCombo.SelectedItem as AdapterInfo)?.Guid;
        _settings.EthernetAdapterGuid = (EthCombo.SelectedItem as AdapterInfo)?.Guid;
        _settings.DisableOppositeOnSwitch = DisableOppositeToggle.IsChecked == true;
        _settings.AutoSwitch = AutoToggle.IsChecked == true;

        try
        {
            _startup.SetEnabled(StartupToggle.IsChecked == true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "NetSwitch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _settingsService.Save(_settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Header_DragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
