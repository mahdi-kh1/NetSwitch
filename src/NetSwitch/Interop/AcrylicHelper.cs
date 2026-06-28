using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NetSwitch.Interop;

/// <summary>
/// Enables the Windows acrylic "blur-behind" effect on a WPF window so the
/// glassmorphism panels actually blur whatever is behind them.
/// </summary>
public static class AcrylicHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    // ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
    private const int AccentEnableAcrylicBlurBehind = 4;
    // WCA_ACCENT_POLICY = 19
    private const int WcaAccentPolicy = 19;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>
    /// Apply acrylic blur. <paramref name="tintArgb"/> is the tint applied over the
    /// blur in 0xAABBGGRR order (note: BGR, not RGB).
    /// </summary>
    public static void EnableAcrylic(Window window, uint tintArgb = 0x99201A16)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var accent = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = 2, // draw all borders
            GradientColor = tintArgb
        };

        var size = Marshal.SizeOf(accent);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = ptr,
                SizeOfData = size
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
