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
        var image = PresentationTheme.AppIcon(32);
        PresentationTheme.Apply(image, new DisplayPreferences { Theme = ColorTheme.Light });
        image.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 111, 214)));
        image.Measure(new System.Windows.Size(32, 32));
        image.Arrange(new System.Windows.Rect(0, 0, 32, 32));
        image.UpdateLayout();
        var rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(32, 32, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rendered.Render(image);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rendered));
        using var stream = new MemoryStream(); encoder.Save(stream); stream.Position = 0;
        using var bitmap = new Bitmap(stream);
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
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
    private static extern bool DestroyIcon(IntPtr icon);
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
