using System.Net.NetworkInformation;
using NetSwitch.Models;

namespace NetSwitch.Services;

/// <summary>
/// Watches the Ethernet link and, when auto mode is on, prefers Ethernet while a
/// cable is connected (disabling Wi-Fi) and re-enables Wi-Fi when it is unplugged.
///
/// The Ethernet adapter is intentionally kept enabled so it can act as the cable
/// sensor — only Wi-Fi is toggled.
/// </summary>
public sealed class AutoSwitchMonitor
{
    private readonly SwitchController _controller;
    private readonly NetworkService _network;
    private readonly AppSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _running;
    private bool? _lastEthernetConnected;

    public event EventHandler? Switched;

    public AutoSwitchMonitor(SwitchController controller, AppSettings settings)
    {
        _controller = controller;
        _settings = settings;
        _network = new NetworkService();
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _lastEthernetConnected = null;
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        _ = EvaluateAsync();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }

    private async void OnNetworkChanged(object? sender, EventArgs e) => await EvaluateAsync();

    private async Task EvaluateAsync()
    {
        if (!_settings.AutoSwitch) return;

        await _gate.WaitAsync();
        try
        {
            var didSwitch = await Task.Run(Evaluate);
            if (didSwitch)
                Switched?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <returns>true if an adapter was toggled.</returns>
    private bool Evaluate()
    {
        var eth = _controller.ResolveEthernet(_settings);
        if (eth is null)
            return false;

        // Keep Ethernet enabled so we can sense the cable. If it was disabled
        // (e.g. user manually switched to Wi-Fi earlier), bring it back and wait
        // for the next event to read a reliable link state.
        if (!eth.IsEnabled)
        {
            _network.Enable(eth.Guid);
            return true;
        }

        var ethConnected = eth.IsConnected;
        if (_lastEthernetConnected == ethConnected)
            return false;

        _lastEthernetConnected = ethConnected;

        var wifi = _controller.ResolveWifi(_settings);
        if (wifi is null)
            return false;

        if (ethConnected)
        {
            // Cable plugged in → prefer Ethernet, turn Wi-Fi off.
            if (wifi.IsEnabled)
            {
                _network.Disable(wifi.Guid);
                return true;
            }
        }
        else
        {
            // Cable unplugged → fall back to Wi-Fi.
            if (!wifi.IsEnabled)
            {
                _network.Enable(wifi.Guid);
                return true;
            }
        }

        return false;
    }
}
