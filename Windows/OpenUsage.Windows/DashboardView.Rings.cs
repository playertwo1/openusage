using W = System.Windows;
using C = System.Windows.Controls;
using M = System.Windows.Media;
using S = System.Windows.Shapes;
using A = System.Windows.Automation;

namespace OpenUsage.Windows;

// Overview: one grouped list, one 48px row per account, expandable into the weekly pace chart.
public sealed partial class DashboardView
{
    private Guid? expanded;

    private void RenderList(IReadOnlyDictionary<Guid, AccountStatus> statuses)
    {
        var rows = new List<W.UIElement>();
        foreach (var account in store.State.Accounts)
        {
            var status = statuses.GetValueOrDefault(account.Id);
            if (display.FavoritesOnly && status?.Snapshot is UsageSnapshot snap && !snap.Metrics.Any(m => store.IsPinned(account, m) && !store.IsHidden(account, m))) continue;
            rows.Add(ListItem(account, status));
        }
        if (rows.Count == 0) { Empty(store.State.Accounts.Count == 0); return; }
        var unit = display.Usage == UsageDisplay.Used ? "used" : "left";
        var header = Columns(new W.GridLength(28), new W.GridLength(1, W.GridUnitType.Star), new W.GridLength(72), new W.GridLength(84), new W.GridLength(10)); header.Margin = new W.Thickness(12, 12, 12, 4);
        var name = Text("Account", 10, "Muted"); name.Margin = new W.Thickness(8, 0, 0, 0); Put(header, name, 1);
        Put(header, WindowLabel("5h " + unit, "S5h"), 2); Put(header, WindowLabel("Week " + unit, "Swk"), 3);
        groups.Children.Add(header);
        var list = new C.StackPanel();
        for (var i = 0; i < rows.Count; i++)
        {
            if (i != 0) { var line = new C.Border { Height = 1 }; line.SetResourceReference(C.Border.BackgroundProperty, "Border"); list.Children.Add(line); }
            list.Children.Add(rows[i]);
        }
        var card = new C.Border { CornerRadius = new W.CornerRadius(10), Child = list, ClipToBounds = true, Margin = new W.Thickness(0, 0, 0, 8) };
        card.SetResourceReference(C.Border.BackgroundProperty, "Surface"); A.AutomationProperties.SetName(card, "Accounts"); groups.Children.Add(card);
    }

    private static W.UIElement WindowLabel(string text, string tone)
    {
        var panel = new C.StackPanel { Orientation = C.Orientation.Horizontal };
        var dot = new S.Ellipse { Width = 8, Height = 8, StrokeThickness = 2, Margin = new W.Thickness(0, 0, 4, 0), VerticalAlignment = W.VerticalAlignment.Center };
        dot.SetResourceReference(S.Shape.StrokeProperty, tone); panel.Children.Add(dot); panel.Children.Add(Text(text, 10, "Muted")); return panel;
    }

    private static Metric? Window(UsageSnapshot? snapshot, string label) => snapshot?.Metrics.FirstOrDefault(m => m.Label == label && m.Text is null && m.Percent is not null);

    private W.UIElement ListItem(Account account, AccountStatus? status)
    {
        var snapshot = status?.Snapshot; var session = Window(snapshot, "Session"); var weekly = Window(snapshot, "Weekly");
        var pace = weekly is null ? null : WeeklyPace.From(weekly, store.State.History.GetValueOrDefault(account.Id) ?? [], DateTimeOffset.UtcNow);
        var open = expanded == account.Id;
        var item = new C.StackPanel(); var shown = AccountName(account);
        var row = Columns(new W.GridLength(28), new W.GridLength(1, W.GridUnitType.Star), new W.GridLength(72), new W.GridLength(84), new W.GridLength(10));
        row.Height = 48; row.Margin = new W.Thickness(12, 0, 12, 0); row.Background = M.Brushes.Transparent;
        var face = Mini(session, weekly); row.Children.Add(face);
        var title = new C.StackPanel { Margin = new W.Thickness(8, 0, 0, 0), VerticalAlignment = W.VerticalAlignment.Center };
        var top = new C.StackPanel { Orientation = C.Orientation.Horizontal };
        var icon = PresentationTheme.ProviderIcon(account.Kind, 12); icon.Margin = new W.Thickness(0, 0, 6, 0); top.Children.Add(icon);
        if (snapshot is not null && snapshot.Metrics.Any(m => store.IsPinned(account, m) && !store.IsHidden(account, m))) { var star = Text("★", 11, "Accent"); star.Margin = new W.Thickness(0, 0, 4, 0); top.Children.Add(star); }
        top.Children.Add(Text(shown, 13, "Foreground", true)); title.Children.Add(top);
        title.Children.Add(Sub(account, status, snapshot, pace, session)); Put(row, title, 1);
        Put(row, Cell(session, null), 2); Put(row, Cell(weekly, pace), 3);
        var chevron = Text(open ? "⌃" : "⌄", 11, "Muted"); chevron.HorizontalAlignment = W.HorizontalAlignment.Right; Put(row, chevron, 4);
        var head = new C.Border { Child = row, Focusable = true, Cursor = W.Input.Cursors.Hand, ContextMenu = RowMenu(account) };
        A.AutomationProperties.SetName(head, $"{account.Kind.Name()} {shown}");
        void Toggle() { expanded = open ? null : account.Id; LayoutChanged?.Invoke(); }
        head.MouseLeftButtonUp += (_, _) => Toggle();
        head.KeyDown += (_, e) => { if (e.Key is W.Input.Key.Enter or W.Input.Key.Space) { Toggle(); e.Handled = true; } };
        item.Children.Add(head);
        if (open) { item.Children.Add(Details(account, status, pace)); item.SetResourceReference(C.Panel.BackgroundProperty, "Raised"); }
        return item;
    }

    private string AccountName(Account account) => MetricPresentation.AccountName(account, store.State.Accounts, display.HideAccountNames);

    private W.UIElement Sub(Account account, AccountStatus? status, UsageSnapshot? snapshot, WeeklyPace? pace, Metric? session)
    {
        var line = new C.StackPanel { Orientation = C.Orientation.Horizontal };
        if (status?.Error is string error) { line.Children.Add(Text(error, 11, "Danger")); return line; }
        if (pace is not null)
        {
            line.Children.Add(Text(pace.Verdict, 11, pace.RunsOut ? "Warning" : "Success"));
            line.Children.Add(Text(" · " + pace.Trend, 11, "Muted"));
        }
        else if (session is not null) line.Children.Add(Text(Window(snapshot, "Weekly") is null ? "5h window" : "Weekly pace needs a reset time", 11, "Muted"));
        else if (snapshot?.Metrics.FirstOrDefault(m => !store.IsHidden(account, m)) is { } other) line.Children.Add(Text($"{other.Label} {MetricPresentation.Value(other, display)}", 11, "Muted"));
        else line.Children.Add(Text(status?.RetryAfter > DateTimeOffset.UtcNow ? "Waiting for rate limit" : "Fetching", 11, "Muted"));
        return line;
    }

    // Number colour is the only alert: red from 85% used, amber when the weekly projection passes 100%.
    private W.UIElement Cell(Metric? metric, WeeklyPace? pace)
    {
        if (metric is null || MetricPresentation.Fill(metric, display) is not double fill) { var none = Text("-", 12, "Muted"); none.HorizontalAlignment = W.HorizontalAlignment.Center; return none; }
        var used = metric.Percent ?? 0; var alert = used >= 85 ? "Danger" : pace?.RunsOut == true ? "Warning" : null;
        var cell = new C.StackPanel { VerticalAlignment = W.VerticalAlignment.Center };
        var line = new C.StackPanel { Orientation = C.Orientation.Horizontal };
        if (alert is not null) { var dot = new S.Ellipse { Width = 5, Height = 5, Margin = new W.Thickness(0, 0, 4, 0) }; dot.SetResourceReference(S.Shape.FillProperty, alert); line.Children.Add(dot); }
        line.Children.Add(Text($"{fill:0}%", 14, alert ?? "Foreground", true));
        if (metric.ResetsAt is DateTimeOffset reset) { var time = Text(Compact(reset), 10, "Muted"); time.Margin = new W.Thickness(4, 0, 0, 0); line.Children.Add(time); }
        cell.Children.Add(line);
        var bar = new C.Grid { Height = 3, Margin = new W.Thickness(0, 4, 0, 0) };
        var track = new C.Border { CornerRadius = new W.CornerRadius(2) }; track.SetResourceReference(C.Border.BackgroundProperty, "Track"); bar.Children.Add(track);
        var fillBar = new C.Border { CornerRadius = new W.CornerRadius(2), HorizontalAlignment = W.HorizontalAlignment.Left }; fillBar.SetResourceReference(C.Border.BackgroundProperty, MetricPresentation.BaseTone(metric.Label)); bar.Children.Add(fillBar);
        C.Border? tick = null;
        if (pace is not null) { tick = new C.Border { Width = 2, Height = 7, CornerRadius = new W.CornerRadius(1), HorizontalAlignment = W.HorizontalAlignment.Left, VerticalAlignment = W.VerticalAlignment.Center }; tick.SetResourceReference(C.Border.BackgroundProperty, "Foreground"); bar.Children.Add(tick); }
        bar.SizeChanged += (_, e) =>
        {
            fillBar.Width = e.NewSize.Width * Math.Clamp(fill, 0, 100) / 100;
            if (tick is not null) tick.Margin = new W.Thickness(Math.Clamp(e.NewSize.Width * (display.Usage == UsageDisplay.Used ? pace!.Expected : 100 - pace!.Expected) / 100 - 1, 0, e.NewSize.Width - 2), 0, 0, 0);
        };
        cell.Children.Add(bar);
        A.AutomationProperties.SetName(cell, $"{metric.Label}: {fill:0}% {(display.Usage == UsageDisplay.Used ? "used" : "left")}");
        return cell;
    }

    internal static string Compact(DateTimeOffset reset)
    {
        var time = reset - DateTimeOffset.UtcNow;
        if (time <= TimeSpan.Zero) return "due";
        return time.TotalDays >= 1 ? $"{(int)time.TotalDays}d {time.Hours}h" : time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes}m" : $"{Math.Max(1, time.Minutes)}m";
    }

    private C.ContextMenu RowMenu(Account account)
    {
        var menu = AccountMenu(account);
        menu.Items.Insert(3, new C.Separator()); Item(menu, "Move Provider Up", () => Save(() => store.MoveProvider(account.Kind, -1))); Item(menu, "Move Provider Down", () => Save(() => store.MoveProvider(account.Kind, 1)));
        return menu;
    }

    private W.UIElement Mini(Metric? session, Metric? weekly)
    {
        const double size = 28, thickness = 3;
        var face = new C.Grid { Width = size, Height = size };
        var single = session is null || weekly is null; var first = session ?? weekly;
        if (first is null) { face.Children.Add(Track(12.5, thickness)); return face; }
        face.Children.Add(Track(12.5, thickness));
        if (MetricPresentation.Fill(first, display) is double outer && outer > 0) face.Children.Add(Arc(size, 12.5, thickness, outer, MetricPresentation.BaseTone(first.Label)));
        if (!single)
        {
            face.Children.Add(Track(7.5, thickness));
            if (MetricPresentation.Fill(weekly!, display) is double inner && inner > 0) face.Children.Add(Arc(size, 7.5, thickness, inner, "Swk"));
        }
        return face;
    }

    internal static S.Path Arc(double size, double radius, double thickness, double fill, string tone)
    {
        var angle = Math.Clamp(fill, 0, 99.99) / 100 * 2 * Math.PI; var c = size / 2;
        var figure = new M.PathFigure(new W.Point(c, c - radius), [new M.ArcSegment(new W.Point(c + radius * Math.Sin(angle), c - radius * Math.Cos(angle)), new W.Size(radius, radius), 0, angle > Math.PI, M.SweepDirection.Clockwise, true)], false);
        var arc = new S.Path { Data = new M.PathGeometry([figure]), StrokeThickness = thickness, StrokeStartLineCap = M.PenLineCap.Round, StrokeEndLineCap = M.PenLineCap.Round };
        arc.SetResourceReference(S.Shape.StrokeProperty, tone); return arc;
    }

    internal static S.Ellipse Track(double radius, double thickness)
    {
        var track = new S.Ellipse { Width = radius * 2, Height = radius * 2, StrokeThickness = thickness, HorizontalAlignment = W.HorizontalAlignment.Center, VerticalAlignment = W.VerticalAlignment.Center };
        track.SetResourceReference(S.Shape.StrokeProperty, "Track"); return track;
    }

    private W.UIElement Details(Account account, AccountStatus? status, WeeklyPace? pace)
    {
        var panel = new C.Grid { Margin = new W.Thickness(50, 0, 12, 10) };
        panel.ColumnDefinitions.Add(new() { Width = new W.GridLength(1, W.GridUnitType.Star) }); panel.ColumnDefinitions.Add(new() { Width = W.GridLength.Auto });
        var text = new C.StackPanel { VerticalAlignment = W.VerticalAlignment.Top };
        if (pace is not null)
        {
            var verdict = Text(pace.RunsOut ? pace.Verdict : "Lasts the week", 12, pace.RunsOut ? "Warning" : "Success", true); text.Children.Add(verdict);
            text.Children.Add(Text($"Up to {pace.PerDayLeft:0}%/day left", 11, "Muted")); text.Children.Add(Text($"Projected {pace.Projection:0}%", 11, "Muted"));
            Put(panel, Chart(pace), 1);
            var daily = Text("Daily  " + string.Join(" · ", pace.Days.Select(d => d.Used is double u ? $"{u:0}" : "-")) + " %", 10, "Muted"); daily.Margin = new W.Thickness(0, 6, 0, 0); text.Children.Add(daily);
        }
        else text.Children.Add(Text("No weekly window to pace for this account.", 11, "Muted"));
        if (status?.FetchedAt is DateTimeOffset fetched) { var updated = Text($"Updated {fetched.ToLocalTime():HH:mm}", 11, "Muted"); updated.Margin = new W.Thickness(0, 6, 0, 0); text.Children.Add(updated); }
        panel.Children.Add(text);
        var more = IconButton("More", "Account Actions", () => { }); more.ContextMenu = AccountMenu(account); more.HorizontalAlignment = W.HorizontalAlignment.Left; more.Margin = new W.Thickness(0, 6, 0, 0);
        more.Click += (_, _) => { more.ContextMenu.PlacementTarget = more; more.ContextMenu.IsOpen = true; }; text.Children.Add(more);
        return panel;
    }

    // Cumulative weekly use against the even-pace diagonal, with the projection to day 7 or to the 100% mark.
    private W.UIElement Chart(WeeklyPace pace)
    {
        const double width = 168, height = 32;
        double X(double day) => day / 7 * width; double Y(double percent) => height - Math.Min(percent, 100) / 100 * height;
        var canvas = new C.Canvas { Width = width, Height = height + 13, Margin = new W.Thickness(12, 0, 0, 0), VerticalAlignment = W.VerticalAlignment.Top };
        var back = new S.Rectangle { Width = width, Height = height, RadiusX = 3, RadiusY = 3, Opacity = .35 }; back.SetResourceReference(S.Shape.FillProperty, "Track"); canvas.Children.Add(back);
        S.Shape Stroke(S.Shape shape, string tone, double thickness, double opacity = 1, M.DoubleCollection? dashes = null) { shape.StrokeThickness = thickness; shape.Opacity = opacity; if (dashes is not null) shape.StrokeDashArray = dashes; shape.SetResourceReference(S.Shape.StrokeProperty, tone); canvas.Children.Add(shape); return shape; }
        Stroke(new S.Line { X1 = 0, Y1 = height, X2 = width, Y2 = 0 }, "Muted", 1, 1, [3, 3]);
        var now = pace.ElapsedHours / 24; var end = pace.RunOutHours is double h ? h / 24 : 7; var endPercent = pace.RunOutHours is null ? pace.Projection : 100;
        Stroke(new S.Line { X1 = X(now), Y1 = Y(pace.Used), X2 = X(end), Y2 = Y(endPercent) }, "Swk", 1.5, .4, [2, 3]);
        if (pace.RunsOut) { var ring = new S.Ellipse { Width = 5, Height = 5 }; ring.StrokeThickness = 1.5; ring.SetResourceReference(S.Shape.StrokeProperty, "Danger"); C.Canvas.SetLeft(ring, X(end) - 2.5); C.Canvas.SetTop(ring, Y(100) - 2.5); canvas.Children.Add(ring); }
        var line = new S.Polyline { Points = new M.PointCollection(pace.Points.Select(p => new W.Point(X(p.Day), Y(p.Percent)))), StrokeLineJoin = M.PenLineJoin.Round };
        Stroke(line, "Swk", 2);
        var dot = new S.Ellipse { Width = 6, Height = 6 }; dot.SetResourceReference(S.Shape.FillProperty, "Accent"); C.Canvas.SetLeft(dot, X(now) - 3); C.Canvas.SetTop(dot, Y(pace.Used) - 3); canvas.Children.Add(dot);
        var today = Math.Clamp((int)now, 0, 6);
        for (var i = 0; i < 7; i++)
        {
            var initial = Text(pace.Start.AddDays(i).ToLocalTime().ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)[..1], 9, i == today ? "Accent" : "Muted", i == today);
            initial.Width = width / 7; initial.TextAlignment = W.TextAlignment.Center; C.Canvas.SetLeft(initial, X(i)); C.Canvas.SetTop(initial, height + 1); canvas.Children.Add(initial);
        }
        A.AutomationProperties.SetName(canvas, $"Weekly pace chart: {pace.Used:0}% used, projected {pace.Projection:0}%");
        return canvas;
    }
}
