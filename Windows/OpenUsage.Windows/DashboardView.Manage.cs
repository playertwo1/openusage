using W = System.Windows;
using C = System.Windows.Controls;
using S = System.Windows.Shapes;
using A = System.Windows.Automation;

namespace OpenUsage.Windows;

// Accounts: grouped by provider, one inset list per account and one 34px row per metric.
public sealed partial class DashboardView
{
    private void RenderManage(IReadOnlyDictionary<Guid, AccountStatus> statuses)
    {
        foreach (var group in store.State.Accounts.GroupBy(a => a.Kind))
        {
            var members = group.ToArray();
            var header = Columns(W.GridLength.Auto, new W.GridLength(1, W.GridUnitType.Star), W.GridLength.Auto, W.GridLength.Auto);
            header.Margin = new W.Thickness(4, 16, 0, 6);
            var icon = PresentationTheme.ProviderIcon(group.Key, 12); icon.Margin = new W.Thickness(0, 0, 6, 0); header.Children.Add(icon);
            Put(header, Text(group.Key.Name(), 11, "Muted", true), 1);
            Put(header, Text(members.Length == 1 ? "1 account" : $"{members.Length} accounts", 11, "Muted"), 2);
            var providerMenu = Menu();
            Item(providerMenu, "Move Provider Up", () => Save(() => store.MoveProvider(group.Key, -1)));
            Item(providerMenu, "Move Provider Down", () => Save(() => store.MoveProvider(group.Key, 1)));
            var providerActions = IconButton("More", group.Key.Name() + " Provider Actions", () => { });
            providerActions.Margin = new W.Thickness(4, 0, 0, 0); providerActions.ContextMenu = providerMenu;
            providerActions.Click += (_, _) => { providerMenu.PlacementTarget = providerActions; providerMenu.IsOpen = true; };
            Put(header, providerActions, 3);
            header.ContextMenu = providerMenu; groups.Children.Add(header);
            foreach (var account in members) groups.Children.Add(AccountList(account, statuses.GetValueOrDefault(account.Id)));
        }
        if (groups.Children.Count == 0) Empty(true);
    }

    private W.UIElement AccountList(Account account, AccountStatus? status)
    {
        var list = new C.StackPanel();
        var head = Columns(new W.GridLength(1, W.GridUnitType.Star), W.GridLength.Auto, W.GridLength.Auto);
        head.Height = 40; head.Margin = new W.Thickness(12, 0, 8, 0);
        var name = Text(account.Label, 13, "Foreground", true); name.Margin = new W.Thickness(0, 0, 8, 0); head.Children.Add(name);
        if (status?.Snapshot?.Plan is string plan)
        {
            var badge = new C.Border { CornerRadius = new W.CornerRadius(4), Padding = new W.Thickness(6, 1, 6, 1), VerticalAlignment = W.VerticalAlignment.Center, Child = Text(plan, 10, "Accent", true) };
            badge.SetResourceReference(C.Border.BackgroundProperty, "AccentSoft"); Put(head, badge, 1);
        }
        var more = IconButton("More", "Account Actions", () => { }); more.ContextMenu = AccountMenu(account); more.Margin = new W.Thickness(6, 0, 0, 0);
        more.Click += (_, _) => { more.ContextMenu.PlacementTarget = more; more.ContextMenu.IsOpen = true; }; Put(head, more, 2);
        list.Children.Add(head);
        if (status?.Error is string error) { var notice = Text(error, 11, "Danger"); notice.TextWrapping = W.TextWrapping.Wrap; notice.Margin = new W.Thickness(12, 0, 12, 8); list.Children.Add(notice); }
        if (status?.Snapshot is UsageSnapshot snapshot)
            foreach (var metric in store.Ordered(account, snapshot)) { list.Children.Add(Line()); list.Children.Add(MetricLine(account, snapshot, metric)); }
        else { list.Children.Add(Line()); var waiting = Text(status?.RetryAfter > DateTimeOffset.UtcNow ? "Waiting for the provider's request limit to reset." : "Fetching your usage…", 11, "Muted"); waiting.Margin = new W.Thickness(12, 8, 12, 8); list.Children.Add(waiting); }
        if (status?.FetchedAt is DateTimeOffset fetched)
        {
            list.Children.Add(Line());
            var stale = status.FetchedAt < DateTimeOffset.UtcNow.AddMinutes(-5) ? " · Out of date" : "";
            var updated = Text($"Updated {fetched.ToLocalTime():HH:mm}{stale}", 10, "Muted"); updated.Margin = new W.Thickness(12, 0, 12, 0); updated.Height = 26; updated.VerticalAlignment = W.VerticalAlignment.Center; list.Children.Add(updated);
        }
        var card = new C.Border { CornerRadius = new W.CornerRadius(10), Child = list, ClipToBounds = true, Margin = new W.Thickness(0, 0, 0, 8) };
        card.SetResourceReference(C.Border.BackgroundProperty, "Surface"); A.AutomationProperties.SetName(card, account.Label + " Metrics"); return card;
    }

    private static C.Border Line() { var line = new C.Border { Height = 1 }; line.SetResourceReference(C.Border.BackgroundProperty, "Border"); return line; }

    private W.UIElement MetricLine(Account account, UsageSnapshot snapshot, Metric metric)
    {
        var row = Columns(new W.GridLength(22), new W.GridLength(1, W.GridUnitType.Star), new W.GridLength(64), new W.GridLength(40), new W.GridLength(64), new W.GridLength(28));
        row.Height = 34; row.Margin = new W.Thickness(8, 0, 12, 0); row.ContextMenu = MetricMenu(account, snapshot, metric);
        row.Children.Add(Favorite(account, snapshot, metric));
        var label = Text(metric.Label, 12, store.IsHidden(account, metric) ? "Muted" : "Foreground"); label.Margin = new W.Thickness(8, 0, 8, 0); Put(row, label, 1);
        var pace = MetricPresentation.IsWeekly(metric) ? WeeklyPace.From(metric, store.State.History.GetValueOrDefault(account.Id) ?? [], DateTimeOffset.UtcNow) : null;
        if (metric.Text is null && metric.Unit == "percent" && MetricPresentation.Fill(metric, display) is double fill)
        {
            var bar = new C.Grid { Height = 3, Margin = new W.Thickness(0, 0, 8, 0) };
            var track = new C.Border { CornerRadius = new W.CornerRadius(2) }; track.SetResourceReference(C.Border.BackgroundProperty, "Track"); bar.Children.Add(track);
            var inner = new C.Border { CornerRadius = new W.CornerRadius(2), HorizontalAlignment = W.HorizontalAlignment.Left }; inner.SetResourceReference(C.Border.BackgroundProperty, MetricPresentation.BaseTone(metric.Label)); bar.Children.Add(inner);
            bar.SizeChanged += (_, e) => inner.Width = e.NewSize.Width * Math.Clamp(fill, 0, 100) / 100;
            A.AutomationProperties.SetName(bar, $"{metric.Label}: {fill:0}% {(display.Usage == UsageDisplay.Used ? "used" : "left")}"); Put(row, bar, 2);
            var alert = metric.Percent >= 85 ? "Danger" : pace?.RunsOut == true ? "Warning" : "Foreground";
            var value = Text($"{fill:0}%", 12, alert, true); value.HorizontalAlignment = W.HorizontalAlignment.Right; Put(row, value, 3);
        }
        else
        {
            var value = Text(metric.Unit == "resets" ? $"{metric.Value:0}" : MetricPresentation.Value(metric, display), 12, metric.Unit == "resets" ? "Muted" : "Foreground", metric.Unit != "resets");
            value.HorizontalAlignment = W.HorizontalAlignment.Right; C.Grid.SetColumnSpan(value, 2); Put(row, value, 2);
        }
        var reset = metric.Unit == "resets" ? "resets" : metric.ResetsAt is DateTimeOffset at ? display.Reset == ResetDisplay.Exact ? at.ToLocalTime().ToString("ddd HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "in " + Compact(at) : "";
        var resetText = Text(reset, 10, "Muted"); resetText.HorizontalAlignment = W.HorizontalAlignment.Right; resetText.Margin = new W.Thickness(0, 0, 8, 0); Put(row, resetText, 4);
        var show = new C.CheckBox { IsChecked = !store.IsHidden(account, metric), HorizontalAlignment = W.HorizontalAlignment.Right, VerticalAlignment = W.VerticalAlignment.Center };
        show.SetResourceReference(StyleProperty, "Switch"); A.AutomationProperties.SetName(show, "Show " + metric.Label + " On Dashboard");
        show.Checked += (_, _) => SetVisible(account, metric, true); show.Unchecked += (_, _) => SetVisible(account, metric, false); Put(row, show, 5);
        A.AutomationProperties.SetName(row, metric.Label + ": " + MetricPresentation.Value(metric, display));
        return row;
    }
}
