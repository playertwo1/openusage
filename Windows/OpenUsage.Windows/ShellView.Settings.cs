using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace OpenUsage.Windows;

public sealed partial class ShellView
{
    private FrameworkElement SettingsPage()
    {
        var page = new StackPanel { Margin = new Thickness(20, 18, 20, 12) };
        var heading = Text("Settings", 15, FontWeights.SemiBold);
        heading.Margin = new Thickness(0, 0, 0, 12); page.Children.Add(heading);

        var appearance = Group(page, "Appearance");
        Row(appearance, "Soft Transparency", "A subtle see-through window; turn off for solid black", Toggle("Soft Transparency", store.State.Display.SoftTransparency,
            value => EditDisplay(display => display.SoftTransparency = value)));
        Row(appearance, "Theme", "Follow Windows or choose your own", ThemeChoice());
        Row(appearance, "Text Size", "Keep the numbers easy to read", Choice("Text Size", new[] { 100, 110, 125 }, store.State.Display.TextSize,
            value => EditDisplay(display => display.TextSize = value), value => $"{value}%"));

        var displayGroup = Group(page, "Usage Display");
        Row(displayGroup, "Show Usage As", null, Choice("Show Usage As", Enum.GetValues<UsageDisplay>(), store.State.Display.Usage,
            value => EditDisplay(display => display.Usage = value)));
        Row(displayGroup, "Reset Times", null, Choice("Reset Times", Enum.GetValues<ResetDisplay>(), store.State.Display.Reset,
            value => EditDisplay(display => display.Reset = value), value => value == ResetDisplay.Countdown ? "Countdown" : "Date & Time"));
        Row(displayGroup, "Favorites Only", "Show only starred metrics in Overview", Toggle("Favorites Only", store.State.Display.FavoritesOnly,
            value => EditDisplay(display => display.FavoritesOnly = value)));
        Row(displayGroup, "Hide Account Names", "Keep your account labels private", Toggle("Hide Account Names", store.State.Display.HideAccountNames,
            value => EditDisplay(display => display.HideAccountNames = value)));

        var general = Group(page, "General");
        Row(general, "Launch At Sign-In", "Keep OpenUsage in the Windows tray", Toggle("Launch At Windows Sign-In", launchAtLogin, value =>
        {
            try { LaunchAtLoginChanged?.Invoke(value); launchAtLogin = value; }
            catch (Exception error)
            {
                AppLog.Error("LaunchAtLogin", error);
                System.Windows.MessageBox.Show(AppLog.Friendly(error), "OpenUsage", MessageBoxButton.OK, MessageBoxImage.Error);
                Navigate("Settings");
            }
        }));
        Row(general, "Low Usage Alerts", "Notify when less than 10% remains", Toggle("Low Usage Alerts", store.State.Notifications, value =>
        {
            try { store.Edit(state => state.Notifications = value); }
            catch (Exception error)
            {
                AppLog.Error("Notifications", error);
                System.Windows.MessageBox.Show(AppLog.Friendly(error), "OpenUsage", MessageBoxButton.OK, MessageBoxImage.Error);
                Navigate("Settings");
            }
        }));
        var shortcut = new StackPanel { Orientation = WpfOrientation.Horizontal };
        foreach (var key in new[] { "Ctrl", "Alt", "U" })
        {
            var cap = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(shortcut.Children.Count == 0 ? 0 : 4, 0, 0, 0), BorderThickness = new Thickness(0, 0, 0, 1), Child = Text(key, 11, FontWeights.Medium) };
            cap.SetResourceReference(Border.BackgroundProperty, "Track"); cap.SetResourceReference(Border.BorderBrushProperty, "Border"); shortcut.Children.Add(cap);
        }
        AutomationProperties.SetName(shortcut, "Global Shortcut Ctrl Alt U");
        Row(general, "Global Shortcut", "Open or hide the panel from anywhere", shortcut);

        Row(general, "Mini Widget", "A small floating panel you can place anywhere", Toggle("Mini Widget", store.State.Display.MiniWidget,
            value => EditDisplay(display => display.MiniWidget = value)));
        Row(general, "Orientation", null, Choice("Mini Widget Orientation", new[] { false, true }, store.State.Display.MiniVertical,
            value => EditDisplay(display => display.MiniVertical = value), value => value ? "Vertical" : "Horizontal"));
        Row(general, "Always On Top", null, Toggle("Mini Widget Always On Top", store.State.Display.MiniOnTop,
            value => EditDisplay(display => display.MiniOnTop = value)));
        Row(general, "Lock Position", null, Toggle("Mini Widget Lock Position", store.State.Display.MiniLocked,
            value => EditDisplay(display => display.MiniLocked = value)));

        var privacy = Text("Accounts stay on this computer. Credentials are protected by your Windows account.", 11);
        privacy.TextWrapping = TextWrapping.Wrap;
        privacy.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        privacy.Margin = new Thickness(0, 8, 0, 14); page.Children.Add(privacy);
        var actions = new WrapPanel { Orientation = WpfOrientation.Horizontal };
        actions.Children.Add(Button("Open Data Folder", "QuietButton", () => OpenDataRequested?.Invoke()));
        var exit = Button("Exit OpenUsage", "QuietButton", () => ExitRequested?.Invoke());
        exit.Margin = new Thickness(6, 0, 0, 0); actions.Children.Add(exit);
        page.Children.Add(actions);
        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private FrameworkElement ThemeChoice()
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Horizontal };
        foreach (var theme in Enum.GetValues<ColorTheme>())
        {
            var button = Button(theme.ToString(), "SegmentButton", () =>
            {
                EditDisplay(display => display.Theme = theme);
                foreach (System.Windows.Controls.Button child in panel.Children) child.Tag = Equals(child.DataContext, theme) ? "selected" : null;
            });
            button.DataContext = theme;
            button.Tag = store.State.Display.Theme == theme ? "selected" : null;
            button.FontSize = 11;
            button.Padding = new Thickness(10, 3, 10, 3);
            panel.Children.Add(button);
        }
        var frame = new Border { Child = panel, CornerRadius = new CornerRadius(7), Padding = new Thickness(2) };
        frame.SetResourceReference(Border.BackgroundProperty, "Track"); return frame;
    }

    private static StackPanel Group(StackPanel page, string title)
    {
        var heading = Text(title, 11, FontWeights.SemiBold);
        heading.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        heading.Margin = new Thickness(4, 0, 0, 6); page.Children.Add(heading);
        var rows = new StackPanel();
        var surface = new Border { Child = rows, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 0, 16) };
        surface.SetResourceReference(Border.BackgroundProperty, "Surface");
        page.Children.Add(surface);
        return rows;
    }
    private static void Row(StackPanel group, string label, string? hint, FrameworkElement control)
    {
        if (group.Children.Count > 0)
        {
            var separator = new Border { Height = 1 };
            separator.SetResourceReference(Border.BackgroundProperty, "Border");
            group.Children.Add(separator);
        }
        var grid = new Grid { MinHeight = 44 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(Text(label, 12, FontWeights.Medium));
        if (hint is not null)
        {
            var description = Text(hint, 11);
            description.TextWrapping = TextWrapping.Wrap;
            description.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            description.Margin = new Thickness(0, 1, 0, 0);
            labels.Children.Add(description);
        }
        grid.Children.Add(labels);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1); grid.Children.Add(control);
        group.Children.Add(grid);
    }
    private static FrameworkElement Choice<T>(string name, IEnumerable<T> values, T selected, Action<T> changed, Func<T, string>? label = null)
    {
        var choice = new WpfComboBox { MinWidth = 76, FontSize = 12, Height = 22 };
        choice.SetResourceReference(StyleProperty, "Choice");
        AutomationProperties.SetName(choice, name);
        foreach (var value in values)
        {
            var item = new ComboBoxItem { Content = label?.Invoke(value) ?? value?.ToString(), Tag = value };
            choice.Items.Add(item);
            if (EqualityComparer<T>.Default.Equals(value, selected)) choice.SelectedItem = item;
        }
        choice.SelectionChanged += (_, _) => { if (choice.SelectedItem is ComboBoxItem { Tag: T value }) changed(value); };
        return choice;
    }
    private static WpfCheckBox Toggle(string name, bool selected, Action<bool> changed)
    {
        var toggle = new WpfCheckBox { IsChecked = selected, VerticalAlignment = VerticalAlignment.Center };
        toggle.SetResourceReference(StyleProperty, "Switch");
        AutomationProperties.SetName(toggle, name);
        toggle.Checked += (_, _) => changed(true);
        toggle.Unchecked += (_, _) => changed(false);
        return toggle;
    }
}
