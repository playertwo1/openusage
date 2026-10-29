namespace OpenUsage.Windows;

public sealed class MainForm : Form
{
    private readonly AccountStore store;
    private readonly Api api;
    private readonly UsageService usage;
    private readonly ProviderLogin login;
    private readonly Dictionary<Guid, AccountStatus> statuses = [];
    private readonly Dictionary<string, bool> notified = [];
    private readonly Dictionary<Guid, CancellationTokenSource> activeRequests = [];
    private readonly ShellView shell;
    private DashboardView dashboard => shell.Dashboard;
    private DashboardView accounts => shell.Accounts;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 5 * 60 * 1000 };
    private readonly System.Windows.Forms.Timer secondaryTimer = new() { Interval = 150 * 1000 };
    private readonly CancellationTokenSource lifetime = new();
    private string statusText = "Refreshes every five minutes";
    private bool refreshing;
    private bool exiting;
    private bool hotkeyRegistered;
    private MiniWidgetForm? mini;
    private readonly bool background;

    public MainForm(AccountStore store, Api api, bool background = false)
    {
        this.store = store; this.api = api; this.background = background;
        usage = new UsageService(api); login = new ProviderLogin(api);
        Text = "OpenUsage";
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(3);
        ClientSize = new Size(440, 700);
        MinimumSize = new Size(400, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = WindowsIntegration.CreateAppIcon();
        shell = new ShellView(store, WindowsIntegration.LaunchAtLogin);
        Controls.Add(new System.Windows.Forms.Integration.ElementHost { Dock = DockStyle.Fill, Child = shell, BackColor = Color.White });
        shell.AddRequested += AddAccount;
        shell.RefreshRequested += () => _ = RefreshAsync(force: true);
        shell.HideRequested += Hide;
        shell.ExitRequested += () => { exiting = true; Close(); };
        shell.DisplayChanged += () => { ApplyAppearance(); Render(); };
        shell.OpenDataRequested += () => { try { WindowsIntegration.OpenDataFolder(); } catch (Exception error) { ShowError(error); } };
        shell.LaunchAtLoginChanged += value => WindowsIntegration.LaunchAtLogin = value;
        shell.DragRequested += () => { WindowsIntegration.ReleaseCapture(); WindowsIntegration.SendMessage(Handle, 0x00A1, 2, 0); };
        ApplyAppearance(resize: true);
        tray = new NotifyIcon { Icon = Icon, Text = "OpenUsage", Visible = true, ContextMenuStrip = trayMenu };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
        tray.BalloonTipClicked += (_, _) => ShowPanel();
        foreach (var view in new[] { dashboard, accounts })
        {
            view.AddRequested += AddAccount;
            view.RenameRequested += Rename;
            view.RemoveRequested += Remove;
            view.LayoutChanged += () => { shell.RefreshPreferences(); Render(); };
        }
        timer.Tick += (_, _) => _ = RefreshAsync();
        secondaryTimer.Tick += (_, _) => { secondaryTimer.Stop(); _ = RefreshSecondaryAsync(); };
        Shown += (_, _) =>
        {
            if (background) Hide();
            timer.Start();
            _ = RefreshAsync();
        };
        FormClosing += (_, e) =>
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else lifetime.Cancel();
        };
        Render();
    }

    private void ApplyAppearance(bool resize = false)
    {
        var brush = (System.Windows.Media.SolidColorBrush)PresentationTheme.Brush(shell, "Background");
        BackColor = Color.FromArgb(brush.Color.R, brush.Color.G, brush.Color.B);
        if (Controls.Count > 0) Controls[0].BackColor = BackColor;
        var scale = store.State.Display.TextSize / 100d;
        var minimum = (int)(400 * scale);
        MinimumSize = new Size(minimum, 420);
        if (resize || Width < minimum) Width = (int)(440 * scale);
    }

    private void AddAccount()
    {
        using var dialog = new AddAccountForm(store, api);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        Render();
        _ = RefreshAsync();
    }
    private void Rename(Account account)
    {
        using var dialog = new Form { Text = "Rename Account", ClientSize = new Size(390, 185), AutoScaleMode = AutoScaleMode.Dpi, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent };
        var content = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24) };
        content.Children.Add(DialogSurface.Text("Account Name", 13));
        var input = new System.Windows.Controls.TextBox { Text = account.Label, MaxLength = 100, Margin = new System.Windows.Thickness(0, 0, 0, 16) };
        System.Windows.Automation.AutomationProperties.SetName(input, "Account Name"); content.Children.Add(input);
        var buttons = new System.Windows.Controls.WrapPanel();
        buttons.Children.Add(DialogSurface.Button("Save", () => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = DialogResult.OK; }, true));
        buttons.Children.Add(DialogSurface.Button("Cancel", dialog.Close)); content.Children.Add(buttons);
        DialogSurface.Host(dialog, content, store.State.Display);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { store.Rename(account.Id, input.Text.Trim()); Render(); }
        catch (Exception error) { AppLog.Error("Rename", error, account.Id); ShowError(error); }
    }
    private void Remove(Account account)
    {
        if (MessageBox.Show(this, $"Remove {account.Label} And Its Saved Credential?", "Remove Account", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            store.Remove(account.Id);
            if (activeRequests.TryGetValue(account.Id, out var active)) active.Cancel();
            statuses.Remove(account.Id);
            Render();
        }
        catch (Exception error) { AppLog.Error("Remove", error, account.Id); ShowError(error); }
    }

    private async Task RefreshAsync(bool force = false)
    {
        if (refreshing || lifetime.IsCancellationRequested) return;
        timer.Stop();
        secondaryTimer.Stop();
        refreshing = true; statusText = "Refreshing usage…"; Render();
        try
        {
            foreach (var original in store.State.Accounts.ToArray())
            {
                if (lifetime.IsCancellationRequested) break;
                var account = store.State.Accounts.FirstOrDefault(a => a.Id == original.Id);
                if (account is null) continue;
                if (!statuses.TryGetValue(account.Id, out var state))
                {
                    statuses[account.Id] = state = new AccountStatus();
                    if (store.State.RetryAfter.TryGetValue(account.Id, out var saved) && saved > DateTimeOffset.UtcNow)
                    {
                        state.RetryAfter = saved;
                        state.Error = $"The Provider Is Rate Limiting This Account. Waiting Until {saved.ToLocalTime():HH:mm}.";
                    }
                }
                if (state.RetryAfter > DateTimeOffset.UtcNow) continue;
                if (!force && state.FetchedAt > DateTimeOffset.UtcNow.AddMinutes(-5)) continue;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                activeRequests[account.Id] = timeout;
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                try
                {
                    var credential = account.Credential;
                    string? renewalWarning = null;
                    if (ProviderLogin.NeedsRefresh(account.Kind, credential, DateTimeOffset.UtcNow))
                    {
                        try { credential = await RenewAsync(account, timeout.Token); }
                        catch (Exception error) when (account.Kind == ProviderKind.Cursor && credential.ExpiresAt > DateTimeOffset.UtcNow
                            && !timeout.IsCancellationRequested && error is ProviderException or HttpRequestException or TaskCanceledException)
                        {
                            AppLog.Error("EarlyTokenRenewal", error, account.Id);
                            renewalWarning = "Early Sign-In Renewal Failed. Your Current Token Still Works; OpenUsage Will Try Again On The Next Refresh.";
                        }
                    }
                    UsageSnapshot snapshot;
                    try { snapshot = await usage.FetchAsync(account.Kind, credential, timeout.Token); }
                    catch (ProviderException error) when (error.StatusCode == 401 && credential.RefreshToken is not null)
                    {
                        var latest = store.State.Accounts.FirstOrDefault(a => a.Id == account.Id);
                        if (latest is null) continue;
                        credential = await RenewAsync(latest, timeout.Token);
                        snapshot = await usage.FetchAsync(account.Kind, credential, timeout.Token);
                    }
                    if (!store.State.Accounts.Any(a => a.Id == account.Id)) continue;
                    state.Snapshot = snapshot; state.FetchedAt = DateTimeOffset.UtcNow; state.Error = renewalWarning; state.RetryAfter = null;
                    store.RecordWeekly(account, snapshot, DateTimeOffset.UtcNow);
                    Notify(account, snapshot);
                }
                catch (Exception error)
                {
                    if (lifetime.IsCancellationRequested) break;
                    AppLog.Error("Refresh", error, account.Id);
                    state.Error = AppLog.Friendly(error);
                    if (error is ProviderException { StatusCode: 429 } limited) SaveBackoff(account, state, limited);
                }
                finally { activeRequests.Remove(account.Id); }
                Render();
            }
        }
        finally
        {
            refreshing = false;
            if (!IsDisposed && !lifetime.IsCancellationRequested)
            {
                statusText = hotkeyRegistered ? "Refreshes every five minutes" : "Shortcut in use · Open from the tray";
                secondaryTimer.Start();
                timer.Start();
                Render();
            }
        }
    }
    private async Task RefreshSecondaryAsync()
    {
        if (refreshing || lifetime.IsCancellationRequested) return;
        refreshing = true;
        try
        {
            foreach (var original in store.State.Accounts.Where(a => a.Kind == ProviderKind.Claude).ToArray())
            {
                var account = store.State.Accounts.FirstOrDefault(a => a.Id == original.Id);
                if (account is null || !statuses.TryGetValue(account.Id, out var state) || state.RetryAfter > DateTimeOffset.UtcNow) continue;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                activeRequests[account.Id] = timeout;
                timeout.CancelAfter(TimeSpan.FromMinutes(1));
                try
                {
                    var snapshot = await usage.RefreshSecondaryAsync(account.Kind, account.Credential, timeout.Token);
                    if (snapshot is not null && store.State.Accounts.Any(a => a.Id == account.Id)) state.Snapshot = snapshot;
                }
                catch (Exception error)
                {
                    if (lifetime.IsCancellationRequested) break;
                    AppLog.Error("SecondaryRefresh", error, account.Id);
                    state.Error = AppLog.Friendly(error);
                    if (error is ProviderException { StatusCode: 429 } limited) SaveBackoff(account, state, limited);
                }
                finally { activeRequests.Remove(account.Id); }
            }
        }
        finally { refreshing = false; if (!IsDisposed && !lifetime.IsCancellationRequested) Render(); }
    }
    private void SaveBackoff(Account account, AccountStatus state, ProviderException error)
    {
        state.RetryAfter = error.RetryAfter ?? DateTimeOffset.UtcNow.AddMinutes(account.Kind == ProviderKind.Claude ? 60 : 5);
        try
        {
            if (store.State.Accounts.Any(a => a.Id == account.Id)) store.Edit(s => s.RetryAfter[account.Id] = state.RetryAfter.Value);
        }
        catch (Exception saveError)
        {
            AppLog.Error("SaveBackoff", saveError, account.Id);
            state.Error += " The Wait Could Not Be Saved. Keep The App Open Until It Ends.";
        }
    }
    private async Task<Credential> RenewAsync(Account account, CancellationToken ct)
    {
        var credential = await login.RefreshAsync(account.Kind, account.Credential, ct);
        var latest = store.State.Accounts.FirstOrDefault(a => a.Id == account.Id);
        if (latest is not null) store.Replace(latest with { Credential = credential });
        return credential;
    }
    private void Notify(Account account, UsageSnapshot snapshot)
    {
        foreach (var metric in snapshot.Metrics)
        {
            var key = AccountStore.MetricKey(account.Id, metric.Label);
            var low = metric.Percent >= 90;
            if (store.State.Notifications && !store.IsHidden(account, metric) && low && !notified.GetValueOrDefault(key))
                tray.ShowBalloonTip(8000, $"{account.Kind.Name()} · {MetricPresentation.AccountName(account, store.State.Accounts, store.State.Display.HideAccountNames)}", $"{metric.Label}: {MetricPresentation.Value(metric, store.State.Display)}", ToolTipIcon.Warning);
            notified[key] = low;
        }
    }
    private void Render()
    {
        if (IsDisposed) return;
        shell.Render(statuses, refreshing, statusText);
        SyncMini();
        foreach (ToolStripItem item in trayMenu.Items.Cast<ToolStripItem>().ToList()) item.Dispose();
        trayMenu.Items.Clear();
        trayMenu.Items.Add("Open OpenUsage", null, (_, _) => ShowPanel());
        trayMenu.Items.Add("Refresh", null, (_, _) => _ = RefreshAsync(force: true));
        trayMenu.Items.Add(store.State.Display.MiniWidget ? "Hide Mini Widget" : "Show Mini Widget", null, (_, _) => ToggleMini());
        trayMenu.Items.Add(new ToolStripSeparator());
        var favorites = new List<string>();
        foreach (var account in store.State.Accounts)
            if (statuses.GetValueOrDefault(account.Id)?.Snapshot is UsageSnapshot snapshot)
                foreach (var metric in store.Ordered(account, snapshot).Where(m => store.IsPinned(account, m)))
                {
                    var text = $"{account.Kind.Name()} · {MetricPresentation.AccountName(account, store.State.Accounts, store.State.Display.HideAccountNames)} · {metric.Label}: {MetricPresentation.Value(metric, store.State.Display)}";
                    favorites.Add(text);
                    trayMenu.Items.Add(text, null, (_, _) => ShowPanel());
                }
        if (favorites.Count != 0) trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => { exiting = true; Close(); });
        var summary = favorites.Count == 0 ? "OpenUsage" : string.Join(" | ", favorites);
        tray.Text = summary.Length > 63 ? summary[..60] + "…" : summary;
    }
    private void Toggle() { if (Visible) Hide(); else ShowPanel(); }
    private void ToggleMini() => EditMini(d => d.MiniWidget = !d.MiniWidget);
    private void EditMini(Action<DisplayPreferences> change)
    {
        try { store.Edit(state => change(state.Display)); shell.RefreshPreferences(); Render(); }
        catch (Exception error) { AppLog.Error("MiniWidget", error); ShowError(error); }
    }
    private void SyncMini()
    {
        if (!store.State.Display.MiniWidget) { if (mini is not null) { mini.Dispose(); mini = null; } return; }
        if (mini is null)
        {
            mini = new MiniWidgetForm(store);
            mini.OpenRequested += ShowPanel; mini.RefreshRequested += () => _ = RefreshAsync(force: true);
            mini.HideRequested += ToggleMini; mini.PreferenceChanged += EditMini;
        }
        mini.Render(statuses, statusText);
    }
    private void ShowPanel()
    {
        Show(); WindowState = FormWindowState.Normal;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(Math.Max(area.Left, area.Right - Width - 12), Math.Max(area.Top, area.Bottom - Height - 12));
        Activate();
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        hotkeyRegistered = WindowsIntegration.RegisterHotKey(Handle, 1, 0x4003, (uint)Keys.U);
        if (!hotkeyRegistered) statusText = "Shortcut in use · Open from the tray";
        WindowsIntegration.RoundWindow(Handle);
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam == 1) Toggle();
        base.WndProc(ref m);
        if (m.Msg == 0x0084 && (int)m.Result == 1)
        {
            var point = PointToClient(Cursor.Position);
            var edge = Math.Max(4, DeviceDpi / 24);
            var left = point.X < edge; var right = point.X >= ClientSize.Width - edge;
            var top = point.Y < edge; var bottom = point.Y >= ClientSize.Height - edge;
            if (top && left) m.Result = 13; else if (top && right) m.Result = 14;
            else if (bottom && left) m.Result = 16; else if (bottom && right) m.Result = 17;
            else if (left) m.Result = 10; else if (right) m.Result = 11;
            else if (top) m.Result = 12; else if (bottom) m.Result = 15;
        }
    }
    private void ShowError(Exception error) => MessageBox.Show(this, AppLog.Friendly(error), "OpenUsage", MessageBoxButtons.OK, MessageBoxIcon.Error);
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lifetime.Cancel(); timer.Stop(); timer.Dispose(); secondaryTimer.Stop(); secondaryTimer.Dispose();
            if (hotkeyRegistered) WindowsIntegration.UnregisterHotKey(Handle, 1);
            mini?.Dispose();
            tray.Visible = false; tray.Dispose(); trayMenu.Dispose(); lifetime.Dispose();
        }
        base.Dispose(disposing);
    }
}
