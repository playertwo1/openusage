using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenUsage.Windows;

internal static class WindowsIntegration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool LaunchAtLogin
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("OpenUsage.Windows") is string; }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue("OpenUsage.Windows", $"\"{Environment.ProcessPath}\" --background");
            else key.DeleteValue("OpenUsage.Windows", throwOnMissingValue: false);
        }
    }
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidOperationException("Only HTTPS Links Can Be Opened.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    public static void OpenDataFolder()
    {
        Directory.CreateDirectory(AccountStore.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", AccountStore.DataDirectory) { UseShellExecute = true });
    }
    public static Icon CreateAppIcon()
    {
        using var stream = typeof(WindowsIntegration).Assembly.GetManifestResourceStream("OpenUsage.AppIcon.ico")
            ?? throw new InvalidOperationException("Missing Application Icon.");
        return new Icon(stream, SystemInformation.IconSize);
    }
    public static void RoundWindow(IntPtr handle)
    {
        // Windows 10 ignores the Windows 11 rounded-corner preference.
        var rounded = 2;
        _ = DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr window, int id);
}
