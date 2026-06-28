using NetSwitch.Models;

namespace NetSwitch.Services;

public enum NetworkTarget
{
    Wifi,
    Ethernet
}

/// <summary>A fresh read of the two adapters the user cares about.</summary>
public sealed record NetworkSnapshot(AdapterInfo? Wifi, AdapterInfo? Ethernet)
{
    /// <summary>The adapter currently providing connectivity, if any.</summary>
    public AdapterInfo? Active =>
        Ethernet is { IsConnected: true } ? Ethernet :
        Wifi is { IsConnected: true } ? Wifi :
        null;
}

/// <summary>
/// Orchestrates switching between the configured Wi-Fi and Ethernet adapters,
/// resolving them from settings (falling back to best-effort guesses).
/// </summary>
public sealed class SwitchController
{
    private readonly NetworkService _net;

    public SwitchController(NetworkService net) => _net = net;

    public AdapterInfo? ResolveWifi(AppSettings s) =>
        _net.FindByGuid(s.WifiAdapterGuid) ?? _net.GuessWifi();

    public AdapterInfo? ResolveEthernet(AppSettings s) =>
        _net.FindByGuid(s.EthernetAdapterGuid) ?? _net.GuessEthernet();

    /// <summary>
    /// Resolve both adapters from a single adapter enumeration (one WMI query)
    /// instead of querying separately for each — much snappier on refresh.
    /// </summary>
    public NetworkSnapshot GetSnapshot(AppSettings s)
    {
        var adapters = _net.GetPhysicalAdapters();
        var wifi = adapters.FirstOrDefault(a => Same(a.Guid, s.WifiAdapterGuid))
                   ?? adapters.FirstOrDefault(a => a.IsWireless);
        var eth = adapters.FirstOrDefault(a => Same(a.Guid, s.EthernetAdapterGuid))
                  ?? adapters.FirstOrDefault(a => a.Kind == AdapterKind.Ethernet);
        return new NetworkSnapshot(wifi, eth);
    }

    private static bool Same(string a, string? b) =>
        b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Enable the target adapter and, if configured, disable the opposite one.
    /// </summary>
    public async Task SwitchToAsync(NetworkTarget target, AppSettings s)
    {
        var wifi = ResolveWifi(s);
        var eth = ResolveEthernet(s);
        var (primary, secondary) = target == NetworkTarget.Wifi ? (wifi, eth) : (eth, wifi);

        if (primary is null)
            throw new InvalidOperationException(
                $"No {target} adapter is configured. Pick one in Settings.");

        await _net.EnableAsync(primary.Guid);

        if (s.DisableOppositeOnSwitch && secondary is not null)
            await _net.DisableAsync(secondary.Guid);
    }
}
