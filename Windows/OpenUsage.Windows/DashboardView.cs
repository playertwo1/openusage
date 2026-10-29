using W = System.Windows;
using C = System.Windows.Controls;
using A = System.Windows.Automation;

namespace OpenUsage.Windows;

public sealed class AccountStatus
{
    public UsageSnapshot? Snapshot { get; set; }
    public DateTimeOffset? FetchedAt { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? RetryAfter { get; set; }
}

public sealed partial class DashboardView : C.UserControl
{
    public event Action<Account>? RenameRequested;
    public event Action<Account>? RemoveRequested;
    public event Action? LayoutChanged;
    public event Action? AddRequested;
    private readonly bool manage;
    private readonly C.StackPanel groups = new();
    private readonly C.ScrollViewer scroll;
    private AccountStore store = null!;
    private DisplayPreferences display = new();
    public DashboardView(bool manage = false)
    {
        this.manage = manage;
        scroll = new C.ScrollViewer { Content = groups, VerticalScrollBarVisibility = C.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = C.ScrollBarVisibility.Disabled, Padding = new W.Thickness(22, 4, 22, 18), CanContentScroll = false };
        Content = scroll;
        SetResourceReference(BackgroundProperty, "Background");
    }
    public void Render(AccountStore store, IReadOnlyDictionary<Guid, AccountStatus> statuses)
    {
        this.store = store; display = store.State.Display;
        var offset = scroll.VerticalOffset;
        groups.Children.Clear();
        if (!manage) { RenderList(statuses); scroll.ScrollToVerticalOffset(offset); return; }
        RenderManage(statuses);
        scroll.ScrollToVerticalOffset(offset);
    }
    private C.Button Favorite(Account account, UsageSnapshot snapshot, Metric metric)
    {
        var pinned = store.IsPinned(account, metric);
        var button = IconButton(pinned ? "StarFilled" : "Star", (pinned ? "Unstar " : "Star ") + metric.Label, () => Save(() =>
        {
            if (!pinned && snapshot.Metrics.Count(m => store.IsPinned(account, m)) >= 2) throw new ProviderException("You Can Star Up To Two Metrics Per Account. Unstar One First.");
            store.Edit(state => state.Pins[AccountStore.MetricKey(account.Id, metric.Label)] = !pinned);
        }));
        button.SetResourceReference(ForegroundProperty, pinned ? "Accent" : "Muted"); return button;
    }
    private C.ContextMenu AccountMenu(Account account)
    {
        var menu = Menu(); Item(menu, "Rename Account", () => RenameRequested?.Invoke(account));
        Item(menu, "Move Account Up", () => Save(() => store.MoveAccount(account.Id, -1))); Item(menu, "Move Account Down", () => Save(() => store.MoveAccount(account.Id, 1)));
        menu.Items.Add(new C.Separator()); Item(menu, "Remove Account", () => RemoveRequested?.Invoke(account)); return menu;
    }
    private C.ContextMenu MetricMenu(Account account, UsageSnapshot snapshot, Metric metric)
    {
        var menu = Menu(); Item(menu, "Move Metric Up", () => Save(() => store.MoveMetric(account, snapshot, metric.Label, -1))); Item(menu, "Move Metric Down", () => Save(() => store.MoveMetric(account, snapshot, metric.Label, 1)));
        Item(menu, store.IsHidden(account, metric) ? "Show Metric" : "Hide Metric", () => SetVisible(account, metric, store.IsHidden(account, metric))); return menu;
    }
    private C.ContextMenu Menu()
    {
        var menu = new C.ContextMenu(); PresentationTheme.Apply(menu, display); menu.SetResourceReference(C.Control.BackgroundProperty, "Surface"); menu.SetResourceReference(C.Control.ForegroundProperty, "Foreground"); return menu;
    }
    private static void Item(C.ContextMenu menu, string title, Action action) { var item = new C.MenuItem { Header = title }; item.Click += (_, _) => action(); menu.Items.Add(item); }
    private void SetVisible(Account account, Metric metric, bool shown) => Save(() => store.Edit(state => { var key = AccountStore.MetricKey(account.Id, metric.Label); if (shown) state.Hidden.Remove(key); else state.Hidden.Add(key); }));
    private void Empty(bool first)
    {
        var empty = new C.StackPanel { Margin = new W.Thickness(5, 40, 5, 20) };
        var logos = new C.StackPanel { Orientation = C.Orientation.Horizontal, HorizontalAlignment = W.HorizontalAlignment.Center, Margin = new W.Thickness(0, 0, 0, 20) };
        foreach (var kind in new[] { ProviderKind.Claude, ProviderKind.Codex, ProviderKind.Cursor }) { var icon = PresentationTheme.ProviderIcon(kind, 30); icon.Margin = new W.Thickness(10, 0, 10, 0); logos.Children.Add(icon); }
        empty.Children.Add(logos);
        var title = Text(first ? "Your AI Limits, Together" : "No Favorites To Show", 18, "Foreground", true); title.TextAlignment = W.TextAlignment.Center; empty.Children.Add(title);
        var detail = Text(first ? "Connect your accounts to see limits, balances and reset times in one quiet place." : "Star a metric or switch back to All to see your usage.", 12, "Muted"); detail.TextWrapping = W.TextWrapping.Wrap; detail.TextAlignment = W.TextAlignment.Center; detail.Margin = new W.Thickness(5, 10, 5, 20); empty.Children.Add(detail);
        if (first) { var add = new C.Button { Content = "Add Account", HorizontalAlignment = W.HorizontalAlignment.Center }; add.SetResourceReference(StyleProperty, "PrimaryButton"); add.Click += (_, _) => AddRequested?.Invoke(); empty.Children.Add(add); }
        groups.Children.Add(empty);
    }
    private static C.TextBlock Text(string value, double size, string brush, bool bold = false)
    {
        var text = new C.TextBlock { Text = value, FontSize = size, FontWeight = bold ? W.FontWeights.SemiBold : W.FontWeights.Normal, VerticalAlignment = W.VerticalAlignment.Center, TextTrimming = W.TextTrimming.CharacterEllipsis };
        text.SetResourceReference(C.TextBlock.ForegroundProperty, brush); return text;
    }
    private static C.Button IconButton(string icon, string name, Action action)
    {
        var button = new C.Button { Content = PresentationTheme.Icon(icon, 14), Width = 26, Height = 24, MinHeight = 0, Padding = new W.Thickness(4), VerticalAlignment = W.VerticalAlignment.Center };
        button.SetResourceReference(StyleProperty, "IconButton"); A.AutomationProperties.SetName(button, name); button.Click += (_, _) => action(); return button;
    }
    private static C.Button TextButton(string text, double size)
    {
        var button = new C.Button { Content = text, FontSize = size, Padding = new W.Thickness(0, 2, 0, 2), MinHeight = 0, HorizontalAlignment = W.HorizontalAlignment.Left };
        button.SetResourceReference(StyleProperty, "QuietButton"); return button;
    }
    private static C.Grid Columns(params W.GridLength[] widths) { var grid = new C.Grid(); foreach (var width in widths) grid.ColumnDefinitions.Add(new() { Width = width }); return grid; }
    private static void Put(C.Grid grid, W.UIElement child, int column) { C.Grid.SetColumn(child, column); grid.Children.Add(child); }
    private void Save(Action change)
    {
        try { change(); LayoutChanged?.Invoke(); }
        catch (Exception error) { AppLog.Error("SaveLayout", error); W.MessageBox.Show(AppLog.Friendly(error), "OpenUsage", W.MessageBoxButton.OK, W.MessageBoxImage.Error); LayoutChanged?.Invoke(); }
    }
}
