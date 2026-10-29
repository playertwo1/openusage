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
            Metric? Find(string label) => snapshot.Metrics.FirstOrDefault(m => m.Label == label && m.Text is null && m.Percent is not null && !store.IsHidden(account, m));
            var session = Find("Session"); var weekly = Find("Weekly");
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

    public MiniWidgetView() { MiniParts.Dark(this); }

    public void Render(IReadOnlyList<MiniRow> rows, DisplayPreferences display)
    {
        var vertical = display.MiniVertical;
        var panel = new C.StackPanel { Orientation = vertical ? C.Orientation.Vertical : C.Orientation.Horizontal };
        var grip = MiniParts.Text(vertical ? "⋯" : "⋮⋮", 10, "Muted"); grip.Opacity = .5; grip.HorizontalAlignment = W.HorizontalAlignment.Center;
        grip.Margin = vertical ? new W.Thickness(0, 0, 0, 8) : new W.Thickness(0, 0, 10, 0); panel.Children.Add(grip);
        foreach (var row in rows) panel.Children.Add(Cell(row, display, vertical));
        if (rows.Count == 0) panel.Children.Add(MiniParts.Text("No usage yet", 11, "Muted"));
        var pill = new C.Border { CornerRadius = new W.CornerRadius(14), BorderThickness = new W.Thickness(1), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C, 0x1C, 0x1E)), Cursor = W.Input.Cursors.Arrow,
            Padding = vertical ? new W.Thickness(8, 6, 8, 10) : new W.Thickness(10, 7, 14, 7), Child = panel };
        pill.SetResourceReference(C.Border.BorderBrushProperty, "Border");
        pill.MouseLeftButtonDown += (_, e) => { if (e.ChangedButton == W.Input.MouseButton.Left) PressStarted?.Invoke(); };
        pill.MouseEnter += (_, _) => HoverStarted?.Invoke();
        Content = pill; A.AutomationProperties.SetName(pill, "OpenUsage Mini Widget");
    }

    private static W.UIElement Cell(MiniRow row, DisplayPreferences display, bool vertical)
    {
        var cell = new C.Grid { Width = 28, Margin = vertical ? new W.Thickness(0, 0, 0, 8) : new W.Thickness(0, 0, 10, 0) };
        var stack = new C.StackPanel();
        var label = MiniParts.Text(row.Short, 9, "Muted", true); label.HorizontalAlignment = W.HorizontalAlignment.Center; stack.Children.Add(label);
        stack.Children.Add(Bar(row.Session, display, "S5h")); stack.Children.Add(Bar(row.Weekly, display, "Swk"));
        cell.Children.Add(stack);
        if (row.Alert != "")
        {
            var dot = new S.Ellipse { Width = 6, Height = 6, HorizontalAlignment = W.HorizontalAlignment.Right, VerticalAlignment = W.VerticalAlignment.Top, Margin = new W.Thickness(0, -3, -4, 0) };
            dot.SetResourceReference(S.Shape.FillProperty, row.Alert); cell.Children.Add(dot);
        }
        A.AutomationProperties.SetName(cell, $"{row.Account.Kind.Name()}: " + string.Join(", ", new[] { row.Session, row.Weekly }.OfType<Metric>().Select(m => $"{m.Label} {MetricPresentation.Value(m, display)}")));
        return cell;
    }

    private static W.UIElement Bar(Metric? metric, DisplayPreferences display, string tone)
    {
        var bar = new C.Grid { Height = 4, Margin = new W.Thickness(0, 3, 0, 0) };
        var track = new C.Border { CornerRadius = new W.CornerRadius(2), Opacity = metric is null ? .3 : 1 }; track.SetResourceReference(C.Border.BackgroundProperty, "Track"); bar.Children.Add(track);
        if (metric is not null && MetricPresentation.Fill(metric, display) is double fill)
        {
            var inner = new C.Border { CornerRadius = new W.CornerRadius(2), HorizontalAlignment = W.HorizontalAlignment.Left, Width = 28 * Math.Clamp(fill, 0, 100) / 100 }; inner.SetResourceReference(C.Border.BackgroundProperty, tone); bar.Children.Add(inner);
        }
        return bar;
    }
}

// Hover flyout: one row per account with both windows, reset times and the pace verdict.
public sealed class MiniFlyoutView : C.UserControl
{
    public event Action? OpenRequested;

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
        foreach (var row in rows) panel.Children.Add(Row(store, row, display));
        var foot = new C.Grid { Margin = new W.Thickness(14, 8, 14, 2) };
        foot.Children.Add(MiniParts.Text(status, 10, "Muted"));
        var open = MiniParts.Text("Open OpenUsage ↗", 10, "Accent"); open.HorizontalAlignment = W.HorizontalAlignment.Right; open.Cursor = W.Input.Cursors.Hand;
        open.MouseLeftButtonUp += (_, _) => OpenRequested?.Invoke(); foot.Children.Add(open);
        panel.Children.Add(Rule()); panel.Children.Add(foot);
        var surface = new C.Border { CornerRadius = new W.CornerRadius(8), BorderThickness = new W.Thickness(1), Child = panel, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20)) };
        surface.SetResourceReference(C.Border.BorderBrushProperty, "Border"); Content = surface; A.AutomationProperties.SetName(surface, "OpenUsage Usage Flyout");
    }

    private static W.UIElement Dot(string tone)
    {
        var dot = new S.Ellipse { Width = 7, Height = 7, StrokeThickness = 2, Margin = new W.Thickness(8, 0, 4, 0) }; dot.SetResourceReference(S.Shape.StrokeProperty, tone); return dot;
    }
    private static W.UIElement Rule() { var line = new C.Border { Height = 1, Margin = new W.Thickness(0, 4, 0, 0) }; line.SetResourceReference(C.Border.BackgroundProperty, "Border"); return line; }

    private static W.UIElement Row(AccountStore store, MiniRow row, DisplayPreferences display)
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
        var host = new C.StackPanel(); host.Children.Add(Rule()); host.Children.Add(grid);
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
