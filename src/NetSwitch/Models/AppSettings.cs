namespace NetSwitch.Models;

/// <summary>Persisted user settings (JSON in %AppData%\NetSwitch\settings.json).</summary>
public sealed class AppSettings
{
    /// <summary>Stable GUID of the adapter chosen as Wi-Fi.</summary>
    public string? WifiAdapterGuid { get; set; }

    /// <summary>Stable GUID of the adapter chosen as Ethernet.</summary>
    public string? EthernetAdapterGuid { get; set; }

    /// <summary>When true, auto-prefer Ethernet while a cable is connected.</summary>
    public bool AutoSwitch { get; set; }

    /// <summary>When switching to one side, also disable the other adapter.</summary>
    public bool DisableOppositeOnSwitch { get; set; } = true;

    /// <summary>Launch NetSwitch on Windows sign-in.</summary>
    public bool StartWithWindows { get; set; }
}
