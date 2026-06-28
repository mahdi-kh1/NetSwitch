namespace NetSwitch.Models;

public enum AdapterState
{
    Unknown,
    Disabled,
    Disconnected,
    Connected
}

public enum AdapterKind
{
    Other,
    Wireless,
    Ethernet
}

/// <summary>
/// A physical network adapter as reported by WMI (MSFT_NetAdapter).
/// </summary>
public sealed class AdapterInfo
{
    /// <summary>Connection name, e.g. "Wi-Fi 2" — used as the netsh / display key.</summary>
    public required string Name { get; init; }

    /// <summary>Stable identifier that survives renames, e.g. "{972B98B5-...}".</summary>
    public required string Guid { get; init; }

    /// <summary>Hardware description, e.g. "Realtek PCIe GbE Family Controller".</summary>
    public required string Description { get; init; }

    public AdapterKind Kind { get; init; }
    public AdapterState State { get; init; }

    public bool IsWireless => Kind == AdapterKind.Wireless;
    public bool IsEnabled => State != AdapterState.Disabled;
    public bool IsConnected => State == AdapterState.Connected;

    public string StatusText => State switch
    {
        AdapterState.Connected => "Connected",
        AdapterState.Disconnected => "Disconnected",
        AdapterState.Disabled => "Disabled",
        _ => "Unknown"
    };
}
