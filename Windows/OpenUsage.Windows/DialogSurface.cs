using C = System.Windows.Controls;
using W = System.Windows;

namespace OpenUsage.Windows;

internal static class DialogSurface
{
    public static void Host(Form form, W.FrameworkElement content, DisplayPreferences display)
    {
        var surface = new C.Border { Child = content };
        PresentationTheme.Apply(surface, display);
        surface.SetResourceReference(C.Border.BackgroundProperty, "Background");
        var scale = display.TextSize / 100d;
        surface.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        form.ClientSize = new Size((int)(form.ClientSize.Width * scale), (int)(form.ClientSize.Height * scale));
        form.Controls.Add(new System.Windows.Forms.Integration.ElementHost { Dock = DockStyle.Fill, Child = surface });
        form.KeyPreview = true;
        form.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; form.Close(); } };
    }
    public static C.TextBlock Text(string text, double size = 13, bool muted = false)
    {
        var label = new C.TextBlock { Text = text, FontSize = size, TextWrapping = W.TextWrapping.Wrap, Margin = new W.Thickness(0, 0, 0, 8) };
        label.SetResourceReference(C.TextBlock.ForegroundProperty, muted ? "Muted" : "Foreground"); return label;
    }
    public static C.Button Button(string text, Action action, bool primary = false)
    {
        var button = new C.Button { Content = text, Margin = new W.Thickness(0, 0, 6, 0) };
        button.SetResourceReference(W.FrameworkElement.StyleProperty, primary ? "PrimaryButton" : "QuietButton");
        System.Windows.Automation.AutomationProperties.SetName(button, text);
        button.Click += (_, _) => action(); return button;
    }
}
