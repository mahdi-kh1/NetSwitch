using System.Management;
using NetSwitch.Models;

namespace NetSwitch.Services;

/// <summary>
/// Reads physical network adapters and enables/disables them through WMI
/// (root\StandardCimv2 → MSFT_NetAdapter). Requires Administrator for Enable/Disable.
/// </summary>
public sealed class NetworkService
{
    private const string Scope = @"\\.\root\StandardCimv2";

    // NDIS_PHYSICAL_MEDIUM values that mean "wireless".
    private const uint NdisWirelessLan = 1;
    private const uint NdisNative80211 = 9;

    /// <summary>All physical adapters (includes disabled ones), wireless first.</summary>
    public IReadOnlyList<AdapterInfo> GetPhysicalAdapters()
    {
        var list = new List<AdapterInfo>();
        using var searcher = new ManagementObjectSearcher(
            Scope, "SELECT * FROM MSFT_NetAdapter WHERE ConnectorPresent = true");

        foreach (ManagementBaseObject mo in searcher.Get())
        {
            list.Add(MapAdapter(mo));
            mo.Dispose();
        }

        return list
            .OrderByDescending(a => a.IsWireless)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public AdapterInfo? FindByGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid)) return null;
        return GetPhysicalAdapters()
            .FirstOrDefault(a => string.Equals(a.Guid, guid, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Best-effort default pick for the wireless adapter.</summary>
    public AdapterInfo? GuessWifi() => GetPhysicalAdapters().FirstOrDefault(a => a.IsWireless);

    /// <summary>Best-effort default pick for the wired adapter.</summary>
    public AdapterInfo? GuessEthernet() =>
        GetPhysicalAdapters().FirstOrDefault(a => a.Kind == AdapterKind.Ethernet);

    public void Enable(string guid) => InvokeByGuid(guid, "Enable");
    public void Disable(string guid) => InvokeByGuid(guid, "Disable");

    public Task EnableAsync(string guid) => Task.Run(() => Enable(guid));
    public Task DisableAsync(string guid) => Task.Run(() => Disable(guid));

    private static AdapterInfo MapAdapter(ManagementBaseObject mo)
    {
        var medium = ToUInt(mo["NdisPhysicalMedium"]);
        var adminStatus = ToUInt(mo["InterfaceAdminStatus"]);   // 1 = Up, 2 = Down
        var mediaState = ToUInt(mo["MediaConnectState"]);        // 1 = Connected, 2 = Disconnected

        var kind = medium is NdisWirelessLan or NdisNative80211
            ? AdapterKind.Wireless
            : AdapterKind.Ethernet;

        AdapterState state;
        if (adminStatus != 1)
            state = AdapterState.Disabled;
        else if (mediaState == 1)
            state = AdapterState.Connected;
        else
            state = AdapterState.Disconnected;

        return new AdapterInfo
        {
            Name = mo["Name"]?.ToString() ?? "(unknown)",
            Guid = mo["InterfaceGuid"]?.ToString() ?? string.Empty,
            Description = mo["InterfaceDescription"]?.ToString() ?? string.Empty,
            Kind = kind,
            State = state
        };
    }

    private static void InvokeByGuid(string guid, string method)
    {
        if (string.IsNullOrWhiteSpace(guid))
            throw new ArgumentException("Adapter GUID is empty.", nameof(guid));

        var query = $"SELECT * FROM MSFT_NetAdapter WHERE InterfaceGuid='{guid}'";
        using var searcher = new ManagementObjectSearcher(Scope, query);

        foreach (ManagementBaseObject mo in searcher.Get())
        {
            if (mo is ManagementObject obj)
            {
                // Enable/Disable take no input parameters; the 3-arg overload
                // returns an out-parameters object exposing ReturnValue.
                using var outParams = obj.InvokeMethod(method, null, null);
                obj.Dispose();
                var code = outParams is null ? 0u : ToUInt(outParams["ReturnValue"]);
                if (code != 0)
                    throw new InvalidOperationException(
                        $"{method} failed for adapter {guid} (WMI code {code}).");
                return;
            }
        }

        throw new InvalidOperationException($"Adapter {guid} not found.");
    }

    private static uint ToUInt(object? value)
    {
        if (value is null) return 0;
        try { return Convert.ToUInt32(value); }
        catch { return 0; }
    }
}
