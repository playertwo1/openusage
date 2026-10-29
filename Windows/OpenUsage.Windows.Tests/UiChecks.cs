using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using C = System.Windows.Controls;
using P = System.Windows.Controls.Primitives;

namespace OpenUsage.Windows;

public static class UiChecks
{
    public static void Run(string directory, string? renderPath = null)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { CheckUi(directory, renderPath); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw new Exception("Windows UI Regression Checks Failed", failure);
    }

    // Opens the real MainForm over an empty store, so no account data is touched, and photographs the screen under it.
    public static void WindowSmoke(string directory, string path)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var api = new Api();
                using var form = new MainForm(new AccountStore(Path.Combine(directory, "window")), api);
                form.StartPosition = System.Windows.Forms.FormStartPosition.Manual; form.Location = new System.Drawing.Point(80, 80);
                form.TopMost = true; form.Show(); form.Activate();
                for (var i = 0; i < 30; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(50); }
                using var shot = new System.Drawing.Bitmap(form.Width, form.Height);
                using (var graphics = System.Drawing.Graphics.FromImage(shot)) graphics.CopyFromScreen(form.Location, System.Drawing.Point.Empty, form.Size);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); shot.Save(path);
                Console.WriteLine($"Window Smoke: Size={form.Width}x{form.Height}");
                var miniStore = new AccountStore(Path.Combine(directory, "mini"));
                var one = new Account(Guid.NewGuid(), ProviderKind.Claude, "Work", new Credential("fixture-only"));
                miniStore.Add(one); miniStore.Edit(s => { s.Display.MiniWidget = true; s.Display.MiniX = .5; s.Display.MiniY = .3; });
                var miniStatuses = new Dictionary<Guid, AccountStatus> { [one.Id] = new() { Snapshot = new("Max", [new("Session", 34, ResetsAt: DateTimeOffset.UtcNow.AddHours(3)), new("Weekly", 83, ResetsAt: DateTimeOffset.UtcNow.AddDays(4))]), FetchedAt = DateTimeOffset.UtcNow } };
                using var widget = new MiniWidgetForm(miniStore);
                widget.Render(miniStatuses, "Refreshes every five minutes"); widget.ShowFlyout();
                for (var i = 0; i < 20; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(50); }
                var union = System.Drawing.Rectangle.Union(widget.Bounds, System.Drawing.Rectangle.FromLTRB(widget.Left, widget.Bottom, widget.Left + 420, widget.Bottom + 260));
                using var miniShot = new System.Drawing.Bitmap(union.Width, union.Height);
                using (var graphics = System.Drawing.Graphics.FromImage(miniShot)) graphics.CopyFromScreen(union.Location, System.Drawing.Point.Empty, union.Size);
                miniShot.Save(Path.ChangeExtension(path, ".mini.png"));
                Console.WriteLine($"Mini Widget Smoke: Visible={widget.Visible} Bounds={widget.Bounds}");
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw new Exception("Window Smoke Failed", failure);
    }

    private static void CheckUi(string directory, string? renderPath)
    {
        var store = new AccountStore(directory);
        foreach (var existing in store.State.Accounts.ToArray()) store.Remove(existing.Id);
        var codex = new Account(Guid.NewGuid(), ProviderKind.Codex, "Personal", new Credential("fixture-only"));
        var claude = new Account(Guid.NewGuid(), ProviderKind.Claude, "Work", new Credential("fixture-only-claude"));
        store.Add(codex); store.Add(claude);
        store.Edit(s => s.Display.Theme = ColorTheme.Light);
        var now = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new UsageSnapshot("Pro", [new("Session", 23, ResetsAt: now.AddHours(3)), new("Weekly", 64, ResetsAt: now.AddDays(4)), new("Extra Usage", 100, Unit: "credits")]);
        var statuses = new Dictionary<Guid, AccountStatus>
        {
            [codex.Id] = new() { Snapshot = snapshot, FetchedAt = DateTimeOffset.UtcNow },
            [claude.Id] = new() { Snapshot = new("Max", [new("Session", 34), new("Weekly", 83)]), FetchedAt = DateTimeOffset.UtcNow }
        };
        var week = now.AddDays(-3);
        store.Edit(s => s.History[codex.Id] = [new(week.AddHours(8), 9), new(week.AddDays(1).AddHours(8), 30), new(week.AddDays(2).AddHours(8), 52), new(DateTimeOffset.UtcNow.AddHours(-1), 64)]);
        Check(WeeklyPace.From(snapshot.Metrics[1], store.State.History[codex.Id], now)!.Days[1].Used == 21, "Daily Use Must Be The Difference Between Days");
        var shell = new ShellView(store, false);
        shell.Render(statuses, false, "Updated just now · Refreshes every 5 min");
        shell.Dashboard.LayoutChanged += () => { shell.RefreshPreferences(); shell.Render(statuses, false, "Updated just now"); };
        Render(shell, 440, 700, null);
        var open = Descendants(shell).OfType<C.Border>().First(b => AutomationProperties.GetName(b) == "ChatGPT Personal");
        open.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        Render(shell, 440, 700, renderPath);
        Check(Descendants(shell).OfType<C.TextBlock>().Any(t => t.Text == "23%"), "Usage Values Must Render");
        Check(Descendants(shell).OfType<C.TextBlock>().Any(t => t.Text.StartsWith("Up to ")), "Expanding A Row Must Show The Weekly Pace");
        Check(Descendants(shell).OfType<C.Border>().Count(b => AutomationProperties.GetName(b).Contains(" Personal") || AutomationProperties.GetName(b).Contains(" Work")) == 2, "Every Account Must Have One Row");
        var card = Descendants(shell).OfType<C.Border>().First(b => AutomationProperties.GetName(b) == "Accounts");
        Check(card.ActualWidth >= 370, "Account List Must Fill Available Width");
        Click(Descendants(shell).OfType<C.Button>().First(b => Equals(b.Content, "Remaining")));
        Check(store.State.Display.Usage == UsageDisplay.Remaining, "Remaining Toggle Must Change Display Mode");
        Check(Descendants(shell).OfType<C.TextBlock>().Any(t => t.Text == "77%"), "Remaining Must Show Correct Complement");
        Check(new AccountStore(directory).State.Display.Usage == UsageDisplay.Remaining, "Display Mode Must Persist");
        var behind = WeeklyPace.From(new Metric("Weekly", 3, ResetsAt: now.AddHours(77)), [], now)!;
        Check(Math.Round(behind.Expected) == 54 && Math.Round(behind.Delta) == -51 && !behind.RunsOut && behind.Trend == "-51%", "Pace Behind The Even Line");
        var ahead = WeeklyPace.From(new Metric("Weekly", 55, ResetsAt: now.AddHours(115)), [], now)!;
        Check(Math.Round(ahead.Expected) == 32 && Math.Round(ahead.Delta) == 23 && Math.Round(ahead.Projection) == 174 && ahead.RunsOut && Math.Round(ahead.PerDayLeft) == 9, "Pace Ahead Of The Even Line");
        var gravity = MetricPresentation.Window([new("Gemini 5h", 10), new("Gemini", 20), new("Claude 5h", 30), new("Claude Weekly", 40)], false);
        Check(gravity?.Label == "Gemini 5h" && MetricPresentation.Window([new("Gemini 5h", 10), new("Gemini", 20)], true)?.Label == "Gemini", "Antigravity Windows Must Fill The Rings");
        var miniRows = MiniRow.Build(store, statuses);
        Check(miniRows.Select(r => r.Short).SequenceEqual(new[] { "Cx", "C" }), "Mini Widget Labels Must Follow Provider And Account Order");
        Check(new MiniRow(codex, "Cx", new Metric("Session", 90), null, null).Alert == "Danger", "Mini Widget Must Flag Use From 85 Percent");
        var pill = new MiniWidgetView(); pill.Render(miniRows, store.State.Display);
        Render(pill, 220, 64, Variant(renderPath, "mini"));
        Check(Descendants(pill).OfType<C.TextBlock>().Any(t => t.Text == "Cx") && Descendants(pill).OfType<C.TextBlock>().Any(t => t.Text == "C"), "Mini Widget Must Label Every Account");
        store.Edit(s => s.Display.MiniVertical = true); pill.Render(miniRows, store.State.Display); Render(pill, 64, 180, Variant(renderPath, "mini-vertical")); store.Edit(s => s.Display.MiniVertical = false);
        var flyout = new MiniFlyoutView(); flyout.Render(store, miniRows, DateTimeOffset.UtcNow, "Refreshes every five minutes");
        Render(flyout, 420, 220, Variant(renderPath, "mini-flyout"));
        Check(Descendants(flyout).OfType<C.TextBlock>().Any(t => t.Text == "Open OpenUsage ↗"), "Flyout Must Offer To Open The App");
        var balance = new Metric("Balance", 12, Unit: "usd");
        Check(MetricPresentation.Value(balance, store.State.Display) == balance.Display, "Balance Must Not Be Reversed Or Estimated");
        Check(MetricPresentation.Value(new("Cost", 12, 20, "usd"), store.State.Display) == $"${8d:0.00} left", "Bounded Usage Can Show Remaining");
        Check(MetricPresentation.Fill(new("Session", 23), store.State.Display) == 77, "Bar Must Match Remaining Value");
        Check(MetricPresentation.Reset(now.AddMinutes(30), new(), now) == "Resets in 30m", "Reset Countdown");
        Check(MetricPresentation.Reset(now.AddMinutes(-1), new(), now) == "Reset due", "Expired Reset Label");
        shell.Navigate("Settings"); Render(shell, 440, 700, Variant(renderPath, "settings"));
        var resetTimes = Descendants(shell).OfType<C.ComboBox>().Single(c => AutomationProperties.GetName(c) == "Reset Times");
        resetTimes.SelectedIndex = 1;
        Check(store.State.Display.Reset == ResetDisplay.Exact, "Settings Must Persist Reset Times");
        Check(Descendants(shell).OfType<C.TextBlock>().Any(t => t.Text == "Settings") && !Descendants(shell).OfType<C.TextBlock>().Any(t => t.Text == "Make It Yours"), "Settings Must Use The Short Title");
        var privacy = Descendants(shell).OfType<C.CheckBox>().Single(c => AutomationProperties.GetName(c) == "Hide Account Names");
        privacy.IsChecked = true;
        Check(store.State.Display.HideAccountNames, "Privacy Toggle Must Persist");
        shell.Navigate("Overview"); shell.Navigate("Accounts"); shell.Navigate("Overview");
        Check(!Descendants(shell.Dashboard).OfType<C.TextBlock>().Any(t => t.Text == "Personal"), "Privacy Must Hide Dashboard Account Names");
        Check(MetricPresentation.AccountName(codex, store.State.Accounts, true) == "Account 1", "Overview, Tray And Notifications Must Share Anonymous Labels");
        Check(MetricPresentation.AccountName(codex, store.State.Accounts, false) == "Personal", "Account Label Must Remain Available In Management");
        shell.Navigate("Accounts"); Render(shell, 440, 700, null);
        var providerActions = Descendants(shell.Accounts).OfType<C.Button>().Single(b => AutomationProperties.GetName(b) == "ChatGPT Provider Actions");
        Check(providerActions.Focusable && providerActions.IsTabStop && providerActions.FocusVisualStyle is not null, "Provider Reordering Must Be Keyboard Reachable With Visible Focus");
        var moveDown = providerActions.ContextMenu.Items.OfType<C.MenuItem>().Single(m => Equals(m.Header, "Move Provider Down"));
        moveDown.RaiseEvent(new RoutedEventArgs(C.MenuItem.ClickEvent));
        Check(store.State.Accounts[0].Kind == ProviderKind.Claude, "Provider Menu Commands Must Work Without Mouse Input");
        store.MoveProvider(ProviderKind.Codex, -1);
        shell.Navigate("Accounts"); Render(shell, 440, 700, Variant(renderPath, "accounts"));
        Check(Descendants(shell.Accounts).OfType<C.Button>().Count(b => AutomationProperties.GetName(b).StartsWith("Unstar ")) == 4, "Pin Defaults Must Survive Redesign");
        var shown = Descendants(shell.Accounts).OfType<C.CheckBox>().Where(c => AutomationProperties.GetName(c).EndsWith(" On Dashboard")).ToArray();
        Check(shown.Length == 5 && shown.All(c => c.IsChecked == true), "Every Metric Must Start Shown");
        shown[0].IsChecked = false;
        Check(store.State.Hidden.Count == 1, "Visibility Must Still Be Saved");
        store.Edit(s => { s.Display.Theme = ColorTheme.Dark; s.Display.HideAccountNames = false; s.Hidden.Clear(); });
        shell.RefreshPreferences(); shell.Navigate("Overview"); Render(shell, 440, 700, Variant(renderPath, "dark"));
        Check(((SolidColorBrush)PresentationTheme.Brush(shell, "Background")).Color.R < 80, "Dark Theme Must Actually Apply");
        store.Edit(s => { s.Display.Layout = DashboardLayout.List; s.Display.FavoritesOnly = true; });
        shell.RefreshPreferences(); shell.Render(statuses, false, "Updated just now"); Render(shell, 400, 600, Variant(renderPath, "compact"));
        Check(!Descendants(shell.Dashboard).OfType<C.TextBlock>().Any(t => t.Text == "Extra Usage"), "Favorites Filter Must Exclude Unstarred Metrics");
        store.Edit(s => { s.Display.Theme = ColorTheme.Light; s.Display.TextSize = 125; s.Display.FavoritesOnly = false; s.Display.Layout = DashboardLayout.Cards; });
        shell.RefreshPreferences(); shell.Navigate("Settings"); Render(shell, 500, 700, Variant(renderPath, "large-settings"));
        Check(((ScaleTransform)shell.LayoutTransform).ScaleX == 1.25, "Text Size Must Scale Exactly Once");
        var longAccount = store.State.Accounts.First(a => a.Id == codex.Id) with { Label = "A Very Long Personal Account Name That Must Stay Inside Its Card" };
        store.Replace(longAccount); statuses[codex.Id].Error = "Could not reach the provider. Check your connection and try again.";
        shell.Navigate("Overview"); Render(shell, 500, 700, Variant(renderPath, "error"));
        using (var api = new Api())
        using (var dialog = new AddAccountForm(store, api))
        {
            var surface = dialog.Controls.OfType<System.Windows.Forms.Integration.ElementHost>().Single().Child;
            Render((FrameworkElement)surface, 540, 590, Variant(renderPath, "add-account"));
            Check(Descendants(surface).OfType<C.ComboBox>().Single().Items.Count == 10, "Every Provider Must Remain Available In Account Setup");
            Check(Descendants(surface).OfType<C.TextBlock>().Any(t => t.Text == "Claude"), "Closed Provider Selector Must Name Its Default Selection");
            var provider = Descendants(surface).OfType<C.ComboBox>().Single();
            provider.SelectedIndex = 8;
            Render((FrameworkElement)surface, 540, 590, Variant(renderPath, "add-api-key"));
            Check(Descendants(surface).OfType<C.TextBlock>().Any(t => t.Text == "OpenRouter"), "Closed Provider Selector Must Show A Changed Selection");
        }
        using (var cancellation = new CancellationTokenSource())
        using (var prompt = new LoginPromptForm(new LoginPrompt("Complete sign-in in your browser, then return here.", AcceptsPastedCode: true), cancellation.Token, store.State.Display))
        {
            var surface = prompt.Controls.OfType<System.Windows.Forms.Integration.ElementHost>().Single().Child;
            Render((FrameworkElement)surface, 550, 470, Variant(renderPath, "sign-in"));
            Check(Descendants(surface).OfType<C.TextBox>().Any(t => AutomationProperties.GetName(t) == "Authorization Code"), "Pasted Authorization Code Must Remain Accessible");
            cancellation.Cancel();
            Check(prompt.Result.IsCanceled, "Cancelling Sign-In Must Cancel The Pending UI Task");
        }
        foreach (var kind in Enum.GetValues<ProviderKind>())
        {
            var icon = PresentationTheme.ProviderIcon(kind, 24); PresentationTheme.Apply(icon, store.State.Display);
            Render(icon, 24, 24, null);
        }
        var foreground = ((SolidColorBrush)PresentationTheme.Brush(shell, "Danger")).Color;
        var background = ((SolidColorBrush)PresentationTheme.Brush(shell, "Surface")).Color;
        Check(Contrast(foreground, background) >= 4.5, "Account Error Text Must Meet 4.5:1 Contrast");
        foreach (var account in store.State.Accounts.ToArray()) store.Remove(account.Id);
        store.Edit(s => { s.Display.TextSize = 100; s.Display.Theme = ColorTheme.Light; });
        shell.RefreshPreferences(); shell.Render(statuses, false, "No accounts yet"); Render(shell, 440, 600, Variant(renderPath, "empty"));
        Check(Descendants(shell.Dashboard).OfType<C.Button>().Any(b => Equals(b.Content, "Add Account")), "Empty View Must Offer Account Setup");
    }

    private static void Render(FrameworkElement element, double width, double height, string? path)
    {
        element.Measure(new System.Windows.Size(width, height));
        element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        element.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        var bitmap = new RenderTargetBitmap((int)(width * 1.5), (int)(height * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        Check(pixels.Any(b => b != 0), "Rendered Image Must Not Be Blank");
        if (path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(path); encoder.Save(file);
        }
    }
    private static string? Variant(string? path, string suffix) => path is null ? null : Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-" + suffix + ".png");
    private static void Click(C.Button button) => button.RaiseEvent(new RoutedEventArgs(P.ButtonBase.ClickEvent));
    private static double Contrast(System.Windows.Media.Color a, System.Windows.Media.Color b)
    {
        static double Linear(byte value) { var channel = value / 255d; return channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4); }
        static double Luminance(System.Windows.Media.Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        var first = Luminance(a); var second = Luminance(b); return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
