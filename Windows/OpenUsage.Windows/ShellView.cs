using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace OpenUsage.Windows;

public sealed partial class ShellView : WpfUserControl
{
    public DashboardView Dashboard { get; } = new();
    public DashboardView Accounts { get; } = new(manage: true);
    public event Action? ContentChanged;
    public event Action? AddRequested;
    public event Action? RefreshRequested;
    public event Action? DisplayChanged;
    public event Action? HideRequested;
    public event Action? DragRequested;
    public event Action? ExitRequested;
    public event Action? OpenDataRequested;
    public event Action<bool>? LaunchAtLoginChanged;

    private readonly AccountStore store;
    private bool launchAtLogin;
    private readonly ContentControl pageHost = new();
    private readonly TextBlock footerStatus = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly WpfButton refreshButton;
    private readonly Dictionary<string, WpfButton> navigation = [];
    private readonly Dictionary<UsageDisplay, WpfButton> usageButtons = [];
    private readonly Dictionary<bool, WpfButton> filterButtons = [];
    private readonly StackPanel quickControls = new() { Orientation = WpfOrientation.Horizontal };
    private Grid? overviewPage;
    private IReadOnlyDictionary<Guid, AccountStatus> statuses = new Dictionary<Guid, AccountStatus>();
    private string currentPage = "Overview";
    private string? appearance;

    public double MeasureContentHeight(double width)
    {
        var scale = store.State.Display.TextSize / 100d;
        width /= scale;
        double content;
        if (currentPage == "Settings" && pageHost.Content is ScrollViewer { Content: FrameworkElement settings })
        {
            settings.Measure(new System.Windows.Size(width, double.PositiveInfinity)); content = settings.DesiredSize.Height;
        }
        else content = (currentPage == "Accounts" ? Accounts : Dashboard).MeasureContentHeight(width) + (currentPage == "Overview" ? 46 : 0);
        return (content + 100) * scale;
    }

    public ShellView(AccountStore store, bool launchAtLogin)
    {
        this.store = store;
        this.launchAtLogin = launchAtLogin;
        ApplyAppearance();
        SetResourceReference(BackgroundProperty, "Background");
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(16, 5, 12, 5), Background = System.Windows.Media.Brushes.Transparent };
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == header) DragRequested?.Invoke(); };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nav = new StackPanel { Orientation = WpfOrientation.Horizontal };
        foreach (var page in new[] { "Overview", "Accounts" })
        {
            var button = Button(page, "SegmentButton", () => Navigate(page));
            navigation.Add(page, button); nav.Children.Add(button);
        }
        header.Children.Add(nav);
        var actions = new StackPanel { Orientation = WpfOrientation.Horizontal };
        var mark = Button("", "IconButton", () => HideRequested?.Invoke());
        mark.Content = PresentationTheme.AppIcon(20);
        AutomationProperties.SetName(mark, "Hide OpenUsage"); actions.Children.Add(mark);
        refreshButton = IconButton("Refresh", "Refresh Usage", () => RefreshRequested?.Invoke());
        actions.Children.Add(refreshButton);
        var settings = IconButton("Settings", "Settings", () => Navigate("Settings"));
        navigation.Add("Settings", settings); actions.Children.Add(settings);
        Grid.SetColumn(actions, 1); header.Children.Add(actions); root.Children.Add(header);
        Grid.SetRow(pageHost, 2); root.Children.Add(pageHost);

        var footer = new Grid { Margin = new Thickness(20, 9, 16, 14) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerStatus.FontSize = 11;
        footerStatus.Margin = new Thickness(0, 0, 10, 0);
        footerStatus.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        footer.Children.Add(footerStatus);
        var add = Button("Add Account", "QuietButton", () => AddRequested?.Invoke());
        add.Content = IconLabel("Add", "Add Account");
        Grid.SetColumn(add, 1); footer.Children.Add(add);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;
        Navigate("Overview");
    }

    public void Render(IReadOnlyDictionary<Guid, AccountStatus> statuses, bool refreshing, string statusText)
    {
        ApplyAppearance();
        this.statuses = statuses;
        refreshButton.IsEnabled = !refreshing;
        footerStatus.Text = refreshing ? "Refreshing usage…" : statusText;
        if (currentPage == "Overview") Dashboard.Render(store, statuses);
        else if (currentPage == "Accounts") Accounts.Render(store, statuses);
        UpdateQuickSelection();
        ContentChanged?.Invoke();
    }

    public void Navigate(string page)
    {
        if (!navigation.ContainsKey(page)) throw new ArgumentOutOfRangeException(nameof(page));
        currentPage = page;
        foreach (var (name, button) in navigation)
        {
            button.Tag = name == page ? "selected" : null;
            AutomationProperties.SetItemStatus(button, name == page ? "Selected" : "");
        }
        if (page == "Settings") pageHost.Content = SettingsPage();
        else if (page == "Accounts")
        {
            pageHost.Content = Accounts;
            Accounts.Render(store, statuses);
        }
        else
        {
            if (overviewPage is null)
            {
                overviewPage = new Grid();
                overviewPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                overviewPage.RowDefinitions.Add(new RowDefinition());
                overviewPage.Children.Add(QuickControls());
                Grid.SetRow(Dashboard, 1); overviewPage.Children.Add(Dashboard);
            }
            pageHost.Content = overviewPage;
            Dashboard.Render(store, statuses);
        }
        ContentChanged?.Invoke();
    }

    private FrameworkElement QuickControls()
    {
        var row = new Grid { Margin = new Thickness(20, 10, 20, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        quickControls.Children.Clear(); usageButtons.Clear(); filterButtons.Clear();
        foreach (var mode in Enum.GetValues<UsageDisplay>())
        {
            var button = Button(mode == UsageDisplay.Used ? "Used" : "Remaining", "SegmentButton", () => EditDisplay(display => display.Usage = mode));
            button.FontSize = 11;
            button.Padding = new Thickness(9, 5, 9, 5);
            usageButtons.Add(mode, button); quickControls.Children.Add(button);
        }
        row.Children.Add(quickControls);
        var filters = new StackPanel { Orientation = WpfOrientation.Horizontal };
        foreach (var favorite in new[] { false, true })
        {
            var button = Button(favorite ? "Favorites" : "All", "SegmentButton", () => EditDisplay(display => display.FavoritesOnly = favorite));
            button.FontSize = 11;
            button.Padding = new Thickness(9, 5, 9, 5);
            filterButtons.Add(favorite, button); filters.Children.Add(button);
        }
        Grid.SetColumn(filters, 1); row.Children.Add(filters);
        UpdateQuickSelection();
        return row;
    }

    private void UpdateQuickSelection()
    {
        foreach (var (mode, button) in usageButtons)
        {
            var selected = mode == store.State.Display.Usage;
            button.Tag = selected ? "selected" : null;
            AutomationProperties.SetItemStatus(button, selected ? "Selected" : "");
        }
        foreach (var (favorite, button) in filterButtons)
        {
            var selected = favorite == store.State.Display.FavoritesOnly;
            button.Tag = selected ? "selected" : null;
            AutomationProperties.SetItemStatus(button, selected ? "Selected" : "");
        }
    }

    public void RefreshPreferences()
    {
        ApplyAppearance();
        UpdateQuickSelection();
        if (currentPage == "Settings") pageHost.Content = SettingsPage();
    }

    private void EditDisplay(Action<DisplayPreferences> update)
    {
        try
        {
            store.Edit(state => update(state.Display));
            ApplyAppearance();
            DisplayChanged?.Invoke();
            if (currentPage == "Overview") Dashboard.Render(store, statuses);
            else if (currentPage == "Accounts") Accounts.Render(store, statuses);
            UpdateQuickSelection();
        }
        catch (Exception error)
        {
            AppLog.Error("SaveDisplay", error);
            System.Windows.MessageBox.Show(AppLog.Friendly(error), "OpenUsage", MessageBoxButton.OK, MessageBoxImage.Error);
            Navigate(currentPage);
        }
    }

    private void ApplyAppearance()
    {
        var display = store.State.Display;
        var signature = $"{display.Theme}:{PresentationTheme.IsDark(display)}:{display.TextSize}";
        if (appearance == signature) return;
        PresentationTheme.Apply(this, display);
        var scale = display.TextSize / 100d;
        LayoutTransform = new ScaleTransform(scale, scale);
        appearance = signature;
    }

    private WpfButton IconButton(string icon, string name, Action action)
    {
        var button = Button("", "IconButton", action);
        button.Content = PresentationTheme.Icon(icon, 17);
        AutomationProperties.SetName(button, name);
        return button;
    }
    private static WpfButton Button(string label, string style, Action action)
    {
        var button = new WpfButton { Content = label, VerticalAlignment = VerticalAlignment.Center };
        button.SetResourceReference(StyleProperty, style);
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }
    private static StackPanel IconLabel(string icon, string label)
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Horizontal };
        var image = PresentationTheme.Icon(icon, 14); image.Margin = new Thickness(0, 0, 6, 0);
        panel.Children.Add(image); panel.Children.Add(Text(label, 12));
        return panel;
    }
    private static TextBlock Text(string text, double size = 13, FontWeight? weight = null)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Foreground");
        return block;
    }
}
