using System.Diagnostics;

namespace NetSwitch.Services;

/// <summary>
/// Manages "start with Windows" via a Scheduled Task running at logon with the
/// highest privileges — so the elevated app starts silently (no UAC prompt at boot).
/// </summary>
public sealed class StartupService
{
    private const string TaskName = "NetSwitch";

    public bool IsEnabled() => RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;

    public void SetEnabled(bool enabled)
    {
        if (enabled)
            Enable();
        else
            Disable();
    }

    private void Enable()
    {
        var exe = Environment.ProcessPath
                  ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exe))
            throw new InvalidOperationException("Could not determine the executable path.");

        // /RL HIGHEST = run elevated; /SC ONLOGON = at sign-in; /F = overwrite.
        var args = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\" /SC ONLOGON /RL HIGHEST /F";
        if (RunSchtasks(args) != 0)
            throw new InvalidOperationException("Failed to register the startup task.");
    }

    private void Disable()
    {
        if (IsEnabled())
            RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    private static int RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi);
        if (p is null) return -1;
        p.WaitForExit();
        return p.ExitCode;
    }
}
