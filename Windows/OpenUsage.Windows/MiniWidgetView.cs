using W = System.Windows;
using C = System.Windows.Controls;
using S = System.Windows.Shapes;
using A = System.Windows.Automation;

namespace OpenUsage.Windows;

// One account of the mini widget: its Session and Weekly windows exactly as the provider reports them.
public sealed record MiniRow(Account Account, string Short, Metric? Session, Metric? Weekly, WeeklyPace? Pace)
{
    public string Alert => (Session?.Percent >= 85 || Weekly?.Percent >= 85) ? "Danger" : Pace?.RunsOut == true ? "Warning" : "";

    public static List<MiniRow> Build(AccountStore store, IReadOnlyDictionary<Guid, AccountStatus> statuses)
    {
        var rows = new List<MiniRow>();
        foreach (var account in store.State.Accounts)
        {
            if (statuses.GetValueOrDefault(account.Id)?.Snapshot is not UsageSnapshot snapshot) continue;
            var session = MetricPresentation.Window(snapshot.Metrics, false, m => !store.IsHidden(account, m)); var weekly = MetricPresentation.Window(snapshot.Metrics, true, m => !store.IsHidden(account, m));
            if (session is null && weekly is null) continue;
            var pace = weekly is null ? null : WeeklyPace.From(weekly, store.State.History.GetValueOrDefault(account.Id) ?? [], DateTimeOffset.UtcNow);
            rows.Add(new MiniRow(account, ShortName(account, store.State.Accounts), session, weekly, pace));
        }
        return rows;
    }

    public static string ShortName(Account account, IReadOnlyList<Account> all)
    {
        var same = all.Where(a => a.Kind == account.Kind).ToList();
        var letters = account.Kind switch { ProviderKind.Claude => "C", ProviderKind.Codex => "Cx", ProviderKind.Cursor => "Cu", ProviderKind.Copilot => "Cp", ProviderKind.Antigravity => "AG", ProviderKind.Grok => "Gk", ProviderKind.Ollama => "Ol", ProviderKind.OpenCode => "OC", ProviderKind.OpenRouter => "OR", _ => "Z" };
        return same.Count > 1 ? letters + (same.FindIndex(a => a.Id == account.Id) + 1) : letters;
    }
}

internal static class MiniParts
{
    public static C.TextBlock Text(string value, double size, string brush, bool bold = false)
    {
        var text = new C.TextBlock { Text = value, FontSize = size, FontWeight = bold ? W.FontWeights.SemiBold : W.FontWeights.Normal, VerticalAlignment = W.VerticalAlignment.Center, TextTrimming = W.TextTrimming.CharacterEllipsis };
        text.SetResourceReference(C.TextBlock.ForegroundProperty, brush); return text;
    }
    public static void Dark(W.FrameworkElement root) => PresentationTheme.Apply(root, new DisplayPreferences { Theme = ColorTheme.Dark });
}

// The floating pill: a short label and two thin bars per account, horizontal or vertical.
public sealed class MiniWidgetView : C.UserControl
{
    public event Action? PressStarted;
    public event Action? HoverStarted;
    public event Action? CollapseRequested;

    public MiniWidgetView() { MiniParts.Dark(this); }

    public void Render(IReadOnlyList<MiniRow> rows, DisplayPreferences display, MiniDockEdges dockedEdges = MiniDockEdges.None)
    {
        if (display.MiniCollapsed)
        {
            var iconPillVertical = VerticalFor(display, dockedEdges);
            var providers = rows.Select(r => r.Account.Kind).Distinct().ToArray();
            var icons = new C.StackPanel { Orientation = iconPillVertical ? C.Orientation.Vertical : C.Orientation.Horizontal };
            if (providers.Length == 0) icons.Children.Add(PresentationTheme.AppIcon(20));
            for (var i = 0; i < providers.Length; i++)
            {
                var icon = PresentationTheme.ProviderIcon(providers[i], 20);
                icon.Margin = iconPillVertical ? new W.Thickness(0, 0, 0, i + 1 < providers.Length ? 10 : 0)
                    : new W.Thickness(0, 0, i + 1 < providers.Length ? 10 : 0, 0);
                icons.Children.Add(icon);
            }
            var iconPill = new C.Border
            {
                CornerRadius = DockRadius(dockedEdges), BorderThickness = DockBorder(dockedEdges),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)),
                Padding = iconPillVertical ? new W.Thickness(8, 12, 8, 12) : new W.Thickness(12, 8, 12, 8),
                Child = icons, Focusable = true, Cursor = W.Input.Cursors.Hand
            };
            iconPill.SetResourceReference(C.Border.BorderBrushProperty, "Border");
            iconPill.MouseLeftButtonDown += (_, _) => PressStarted?.Invoke();
            iconPill.MouseEnter += (_, _) => HoverStarted?.Invoke();
            iconPill.KeyDown += (_, e) => { if (e.Key is W.Input.Key.Enter or W.Input.Key.Space) { CollapseRequested?.Invoke(); e.Handled = true; } };
            A.AutomationProperties.SetName(iconPill, providers.Length == 0 ? "Expand Mini Widget"
                : $"Expand Mini Widget. Providers: {string.Join(", ", providers.Select(kind => kind.Name()))}");
            Content = iconPill; return;
        }
        var vertical = display.MiniVertical;
        var panel = new C.StackPanel { Orientation = vertical ? C.Orientation.Vertical : C.Orientation.Horizontal };
        var grip = new C.Button { Content = PresentationTheme.Icon("Minus", 12), Width = 18, Height = 24, MinHeight = 0, Padding = new W.Thickness(0) };
        grip.SetResourceReference(StyleProperty, "IconButton");
        A.AutomationProperties.SetName(grip, "Collapse Mini Widget");
        grip.Click += (_, _) => CollapseRequested?.Invoke();
        grip.Margin = vertical ? new W.Thickness(0, 0, 0, 3) : new W.Thickness(0, 0, 5, 0); panel.Children.Add(grip);
        foreach (var row in rows) panel.Children.Add(Cell(row, display, vertical));
        if (rows.Count == 0) panel.Children.Add(MiniParts.Text("No usage yet", 11, "Muted"));
        var pill = new C.Border { CornerRadius = new W.CornerRadius(14), BorderThickness = new W.Thickness(1), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)), Cursor = W.Input.Cursors.Arrow,
            Padding = new W.Thickness(8, 5, 8, 5), Child = panel };
        pill.SetResourceReference(C.Border.BorderBrushProperty, "Border");
        pill.MouseLeftButtonDown += (_, e) => { if (e.ChangedButton == W.Input.MouseButton.Left) PressStarted?.Invoke(); };
        pill.MouseEnter += (_, _) => HoverStarted?.Invoke();
        Content = pill; A.AutomationProperties.SetName(pill, "OpenUsage Mini Widget");
    }

    internal static bool VerticalFor(DisplayPreferences display, MiniDockEdges dockedEdges)
    {
        if ((dockedEdges & (MiniDockEdges.Top | MiniDockEdges.Bottom)) != 0) return false;
        if ((dockedEdges & (MiniDockEdges.Left | MiniDockEdges.Right)) != 0) return true;
        return display.MiniVertical;
    }

    private static W.CornerRadius DockRadius(MiniDockEdges edges)
    {
        double topLeft = 14, topRight = 14, bottomRight = 14, bottomLeft = 14;
        if ((edges & MiniDockEdges.Top) != 0) topLeft = topRight = 0;
        if ((edges & MiniDockEdges.Bottom) != 0) bottomLeft = bottomRight = 0;
        if ((edges & MiniDockEdges.Left) != 0) topLeft = bottomLeft = 0;
        if ((edges & MiniDockEdges.Right) != 0) topRight = bottomRight = 0;
        return new W.CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
    }

    private static W.Thickness DockBorder(MiniDockEdges edges) => new(
        (edges & MiniDockEdges.Left) == 0 ? 1 : 0,
        (edges & MiniDockEdges.Top) == 0 ? 1 : 0,
        (edges & MiniDockEdges.Right) == 0 ? 1 : 0,
        (edges & MiniDockEdges.Bottom) == 0 ? 1 : 0);

    private static W.UIElement Cell(MiniRow row, DisplayPreferences display, bool vertical)
    {
        var cell = new C.Grid { Width = 134, Margin = vertical ? new W.Thickness(0, 0, 0, 5) : new W.Thickness(0, 0, 7, 0) };
        var stack = new C.StackPanel();
        var ordinal = new string(row.Short.Where(char.IsDigit).ToArray());
        var heading = new C.StackPanel { Orientation = C.Orientation.Horizontal };
        var icon = PresentationTheme.ProviderIcon(row.Account.Kind, 14); icon.Margin = new W.Thickness(0, 0, 5, 0); heading.Children.Add(icon);
        heading.Children.Add(MiniParts.Text(row.Account.Kind.Name() + (ordinal.Length == 0 ? "" : " " + ordinal), 10, "Foreground", true));
        stack.Children.Add(heading);
        stack.Children.Add(Window(row.Session, display, "5h", "S5h"));
        stack.Children.Add(Window(row.Weekly, display, "Week", "Swk"));
        cell.Children.Add(stack);
        A.AutomationProperties.SetName(cell, $"{row.Account.Kind.Name()}: " + string.Join(", ", new[] { row.Session, row.Weekly }.OfType<Metric>().Select(m => $"{m.Label} {MetricPresentation.Value(m, display)}")));
        return cell;
    }

    private static W.UIElement Window(Metric? metric, DisplayPreferences display, string caption, string tone)
    {
        var row = new C.Grid { Margin = new W.Thickness(0, 3, 0, 0) };
        row.ColumnDefinitions.Add(new() { Width = new W.GridLength(27) });
        row.ColumnDefinitions.Add(new() { Width = new W.GridLength(1, W.GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = new W.GridLength(34) });
        var fill = metric is null ? null : MetricPresentation.Fill(metric, display);
        row.Children.Add(MiniParts.Text(caption, 9, "Muted"));
        var bar = Bar(fill, tone); C.Grid.SetColumn(bar, 1); row.Children.Add(bar);
        var value = MiniParts.Text(fill is double percent ? $"{percent:0.#}%" : "—", 10, "Foreground");
        value.HorizontalAlignment = W.HorizontalAlignment.Right; C.Grid.SetColumn(value, 2); row.Children.Add(value); return row;
    }

    private static W.UIElement Bar(double? fill, string tone)
    {
        var bar = new C.Grid { Height = 4, Margin = new W.Thickness(0, 3, 0, 0) };
        var track = new C.Border { CornerRadius = new W.CornerRadius(2), Opacity = fill is null ? .65 : 1 }; track.SetResourceReference(C.Border.BackgroundProperty, "Track"); bar.Children.Add(track);
        if (fill is double percent)
        {
            var inner = new C.Border { CornerRadius = new W.CornerRadius(2), HorizontalAlignment = W.HorizontalAlignment.Left }; inner.SetResourceReference(C.Border.BackgroundProperty, tone); bar.Children.Add(inner);
            bar.SizeChanged += (_, e) => inner.Width = e.NewSize.Width * Math.Clamp(percent, 0, 100) / 100;
        }
        return bar;
    }
}

// Hover flyout: one row per account with both windows, reset times and the pace verdict.
public sealed class MiniFlyoutView : C.UserControl
{
    public event Action? OpenRequested;
    public event Action? ContentChanged;
    private Guid? expanded;

    public MiniFlyoutView() { MiniParts.Dark(this); }

    public void Render(AccountStore store, IReadOnlyList<MiniRow> rows, DateTimeOffset? updated, string status)
    {
        var display = store.State.Display;
        var panel = new C.StackPanel { Width = 400, Margin = new W.Thickness(0, 8, 0, 8) };
        var head = new C.Grid { Margin = new W.Thickness(14, 4, 14, 8) };
        head.Children.Add(MiniParts.Text("OpenUsage", 12, "Foreground", true));
        var legend = new C.StackPanel { Orientation = C.Orientation.Horizontal, HorizontalAlignment = W.HorizontalAlignment.Right };
        legend.Children.Add(Dot("S5h")); legend.Children.Add(MiniParts.Text("5h", 10, "Muted")); legend.Children.Add(Dot("Swk")); legend.Children.Add(MiniParts.Text("Week" + (updated is DateTimeOffset at ? $" · Updated {at.ToLocalTime():HH:mm}" : ""), 10, "Muted"));
        head.Children.Add(legend); panel.Children.Add(head);
        foreach (var row in rows) panel.Children.Add(Row(store, row, display, () =>
        {
            expanded = expanded == row.Account.Id ? null : row.Account.Id;
            Render(store, rows, updated, status); ContentChanged?.Invoke();
        }));
        var foot = new C.Grid { Margin = new W.Thickness(14, 8, 14, 2) };
        foot.Children.Add(MiniParts.Text(status, 10, "Muted"));
        var open = MiniParts.Text("Open OpenUsage ↗", 10, "Accent"); open.HorizontalAlignment = W.HorizontalAlignment.Right; open.Cursor = W.Input.Cursors.Hand;
        open.MouseLeftButtonUp += (_, _) => OpenRequested?.Invoke(); foot.Children.Add(open);
        panel.Children.Add(Rule()); panel.Children.Add(foot);
        var surface = new C.Border { CornerRadius = new W.CornerRadius(8), BorderThickness = new W.Thickness(1), Child = panel, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)) };
        surface.SetResourceReference(C.Border.BorderBrushProperty, "Border");
        Content = new C.ScrollViewer { Content = surface, VerticalScrollBarVisibility = C.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = C.ScrollBarVisibility.Disabled };
        A.AutomationProperties.SetName(surface, "OpenUsage Usage Flyout");
    }

    private static W.UIElement Dot(string tone)
    {
        var dot = new S.Ellipse { Width = 7, Height = 7, StrokeThickness = 2, Margin = new W.Thickness(8, 0, 4, 0) }; dot.SetResourceReference(S.Shape.StrokeProperty, tone); return dot;
    }
    private static W.UIElement Rule() { var line = new C.Border { Height = 1, Margin = new W.Thickness(0, 4, 0, 0) }; line.SetResourceReference(C.Border.BackgroundProperty, "Border"); return line; }

    private W.UIElement Row(AccountStore store, MiniRow row, DisplayPreferences display, Action toggle)
    {
        var grid = new C.Grid { Height = 34, Margin = new W.Thickness(14, 0, 14, 0) };
        foreach (var width in new[] { 22d, 0, 74, 82, 84 }) grid.ColumnDefinitions.Add(new() { Width = width == 0 ? new W.GridLength(1, W.GridUnitType.Star) : new W.GridLength(width) });
        var ring = new C.Grid { Width = 22, Height = 22 };
        var first = row.Session ?? row.Weekly; var single = row.Session is null || row.Weekly is null;
        ring.Children.Add(DashboardView.Track(9, 2.5));
        if (first is not null && MetricPresentation.Fill(first, display) is double outer && outer > 0) ring.Children.Add(DashboardView.Arc(22, 9, 2.5, outer, MetricPresentation.BaseTone(first.Label)));
        if (!single) { ring.Children.Add(DashboardView.Track(5, 2.5)); if (MetricPresentation.Fill(row.Weekly!, display) is double inner && inner > 0) ring.Children.Add(DashboardView.Arc(22, 5, 2.5, inner, "Swk")); }
        grid.Children.Add(ring);
        var name = MiniParts.Text(MetricPresentation.AccountName(row.Account, store.State.Accounts, display.HideAccountNames), 12, "Foreground", true); name.Margin = new W.Thickness(8, 0, 4, 0); Put(grid, name, 1);
        Put(grid, Value(row.Session, display, row.Session?.Percent >= 85 ? "Danger" : "Foreground"), 2);
        Put(grid, Value(row.Weekly, display, row.Weekly?.Percent >= 85 ? "Danger" : row.Pace?.RunsOut == true ? "Warning" : "Foreground"), 3);
        var verdict = MiniParts.Text(row.Pace is null ? (row.Session is not null ? "5h window" : "") : row.Pace.Verdict, 11, row.Pace is null ? "Muted" : row.Pace.RunsOut ? "Warning" : "Success"); verdict.HorizontalAlignment = W.HorizontalAlignment.Right; Put(grid, verdict, 4);
        var host = new C.StackPanel(); host.Children.Add(Rule());
        var button = new C.Button { Content = grid, Padding = new W.Thickness(0), MinHeight = 34, HorizontalContentAlignment = W.HorizontalAlignment.Stretch };
        button.SetResourceReference(StyleProperty, "QuietButton");
        A.AutomationProperties.SetName(button, $"Weekly Chart {row.Account.Id}");
        A.AutomationProperties.SetItemStatus(button, expanded == row.Account.Id ? "Expanded" : "Collapsed");
        button.Click += (_, _) => toggle(); host.Children.Add(button);
        if (expanded == row.Account.Id)
        {
            var details = new C.StackPanel { Margin = new W.Thickness(18, 8, 18, 12) };
            details.Children.Add(MiniParts.Text("Weekly Pace", 12, "Foreground", true));
            if (row.Pace is WeeklyPace pace)
            {
                details.Children.Add(new C.Viewbox { Child = DashboardView.Chart(pace), Margin = new W.Thickness(0, 10, 0, 8) });
                details.Children.Add(MiniParts.Text($"{pace.Used:0}% used · Projected {pace.Projection:0}%", 11, "Muted"));
            }
            else details.Children.Add(MiniParts.Text("No weekly window with a reset time is available.", 11, "Muted"));
            host.Children.Add(details);
        }
        A.AutomationProperties.SetName(host, $"{row.Account.Kind.Name()} {row.Short}");
        return host;
    }

    private static W.UIElement Value(Metric? metric, DisplayPreferences display, string tone)
    {
        if (metric is null || MetricPresentation.Fill(metric, display) is not double fill) return MiniParts.Text("-", 12, "Muted");
        var line = new C.StackPanel { Orientation = C.Orientation.Horizontal };
        line.Children.Add(MiniParts.Text($"{fill:0}%", 12, tone, true));
        if (metric.ResetsAt is DateTimeOffset reset) { var time = MiniParts.Text(DashboardView.Compact(reset), 10, "Muted"); time.Margin = new W.Thickness(4, 0, 0, 0); line.Children.Add(time); }
        return line;
    }
    private static void Put(C.Grid grid, W.UIElement child, int column) { C.Grid.SetColumn(child, column); grid.Children.Add(child); }
}
