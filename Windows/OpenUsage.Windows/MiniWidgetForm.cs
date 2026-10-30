using System.Drawing.Drawing2D;
using System.Windows.Forms.Integration;

namespace OpenUsage.Windows;

// A small borderless window the user places anywhere: drag it, hover for the flyout, right-click for the menu.
public sealed class MiniWidgetForm : Form
{
    private readonly AccountStore store;
    private readonly MiniWidgetView view = new();
    private readonly FlyoutForm flyout;
    private readonly System.Windows.Forms.Timer hover = new() { Interval = 300 };
    private readonly System.Windows.Forms.Timer leave = new() { Interval = 200 };
    private IReadOnlyList<MiniRow> rows = [];
    private DateTimeOffset? updated;
    private string status = "Refreshes every five minutes";
    private DateTime suppressHoverUntil;
    public event Action? OpenRequested;
    public event Action? RefreshRequested;
    public event Action? HideRequested;
    public event Action<Action<DisplayPreferences>>? PreferenceChanged;

    public MiniWidgetForm(AccountStore store)
    {
        this.store = store;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(0, 0, 0);
        Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = view, BackColor = BackColor });
        flyout = new FlyoutForm(store);
        flyout.OpenRequested += () => { flyout.Hide(); OpenRequested?.Invoke(); };
        flyout.ContentChanged += () => { if (flyout.Visible) PositionFlyout(); };
        view.PressStarted += BeginDrag;
        view.CollapseRequested += ToggleCollapsed;
        view.HoverStarted += () => { if (!flyout.Visible && DateTime.UtcNow >= suppressHoverUntil) { hover.Stop(); hover.Start(); } };
        hover.Tick += (_, _) => { hover.Stop(); if (Bounds.Contains(Cursor.Position)) ShowFlyout(); };
        leave.Tick += (_, _) => { if (flyout.Visible && !Bounds.Contains(Cursor.Position) && !flyout.Bounds.Contains(Cursor.Position)) flyout.Hide(); };
        leave.Start();
        view.ContextMenu = BuildMenu();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080; return p; } }

    public void Render(IReadOnlyDictionary<Guid, AccountStatus> statuses, string statusText)
    {
        var display = store.State.Display;
        Opacity = display.SoftTransparency && !System.Windows.SystemParameters.HighContrast ? .90 : 1;
        rows = MiniRow.Build(store, statuses);
        updated = statuses.Values.Select(s => s.FetchedAt).Where(t => t.HasValue).DefaultIfEmpty(null).Max();
        status = statusText;
        view.Render(rows, display); view.ContextMenu = BuildMenu();
        TopMost = display.MiniOnTop;
        if (flyout.Visible) flyout.Render(rows, updated, status);
        Fit();
    }

    // Sizes the window to its content and puts it back inside the work area.
    private void Fit()
    {
        view.UpdateLayout();
        view.InvalidateMeasure();
        view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var scale = DeviceDpi / 96d; var size = view.DesiredSize;
        var wasVisible = Visible;
        ClientSize = new Size((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale));
        using var path = RoundedPath(new Rectangle(0, 0, Width, Height), (int)((store.State.Display.MiniCollapsed ? 16 : 14) * scale));
        Region = new Region(path);
        Place(wasVisible || store.State.Display.MiniWidget);
    }

    private static GraphicsPath RoundedPath(Rectangle area, int radius)
    {
        var d = radius * 2; var path = new GraphicsPath();
        path.AddArc(area.X, area.Y, d, d, 180, 90); path.AddArc(area.Right - d, area.Y, d, d, 270, 90);
        path.AddArc(area.Right - d, area.Bottom - d, d, d, 0, 90); path.AddArc(area.X, area.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }

    private Screen SavedScreen()
    {
        var name = store.State.Display.MiniMonitor;
        return Screen.AllScreens.FirstOrDefault(s => s.DeviceName == name) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }

    // Restores the saved spot relative to its monitor's work area, so a changed resolution keeps it on screen.
    public void Place(bool show)
    {
        var display = store.State.Display; var area = SavedScreen().WorkingArea;
        var x = display.MiniX < 0 ? area.Right - Width - 16 : area.Left + (int)Math.Round(display.MiniX * Math.Max(0, area.Width - Width));
        var y = display.MiniY < 0 ? area.Top + 16 : area.Top + (int)Math.Round(display.MiniY * Math.Max(0, area.Height - Height));
        Location = new Point(Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        if (show && !Visible) Show();
    }

    private void OnDisplaysChanged(object? sender, EventArgs e) { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => Place(Visible)); }

    private void BeginDrag()
    {
        flyout.Hide();
        if (store.State.Display.MiniLocked) { ClickWidget(); return; }
        var before = Location;
        WindowsIntegration.ReleaseCapture(); WindowsIntegration.SendMessage(Handle, 0x00A1, 2, 0);
        if (Location == before) { ClickWidget(); return; }
        Snap(); SavePosition();
    }

    private void ClickWidget() => ToggleCollapsed();
    private void ToggleCollapsed()
    {
        hover.Stop(); flyout.Hide();
        suppressHoverUntil = DateTime.UtcNow.AddSeconds(1);
        PreferenceChanged?.Invoke(d => d.MiniCollapsed = !d.MiniCollapsed);
    }

    // Apply magnetism during native dragging, not just after the mouse is released.
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0x0216 || m.LParam == IntPtr.Zero) return; // WM_MOVING
        var edges = new int[4];
        System.Runtime.InteropServices.Marshal.Copy(m.LParam, edges, 0, 4);
        var bounds = Rectangle.FromLTRB(edges[0], edges[1], edges[2], edges[3]);
        var snapped = SnapBounds(bounds, Screen.FromRectangle(bounds).WorkingArea, DeviceDpi / 96d);
        System.Runtime.InteropServices.Marshal.Copy(new[] { snapped.Left, snapped.Top, snapped.Right, snapped.Bottom }, 0, m.LParam, 4);
        m.Result = (IntPtr)1;
    }

    public static Rectangle SnapBounds(Rectangle bounds, Rectangle area, double scale)
    {
        var reach = (int)Math.Round(32 * scale);
        var gap = (int)Math.Round(6 * scale);
        var x = bounds.Left; var y = bounds.Top;
        if (Math.Abs(x - area.Left) <= reach) x = area.Left + gap;
        else if (Math.Abs(area.Right - bounds.Right) <= reach) x = area.Right - bounds.Width - gap;
        if (Math.Abs(y - area.Top) <= reach) y = area.Top + gap;
        else if (Math.Abs(area.Bottom - bounds.Bottom) <= reach) y = area.Bottom - bounds.Height - gap;
        return new Rectangle(Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - bounds.Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - bounds.Height)), bounds.Width, bounds.Height);
    }

    private void Snap()
    {
        Bounds = SnapBounds(Bounds, Screen.FromRectangle(Bounds).WorkingArea, DeviceDpi / 96d);
    }

    private void SavePosition()
    {
        var screen = Screen.FromRectangle(Bounds); var area = screen.WorkingArea;
        var x = area.Width <= Width ? 0 : (double)(Left - area.Left) / (area.Width - Width);
        var y = area.Height <= Height ? 0 : (double)(Top - area.Top) / (area.Height - Height);
        PreferenceChanged?.Invoke(d => { d.MiniMonitor = screen.DeviceName; d.MiniX = Math.Clamp(x, 0, 1); d.MiniY = Math.Clamp(y, 0, 1); });
    }

    // Opens beside the widget on whichever side has room, always inside the work area.
    public void ShowFlyout()
    {
        flyout.Render(rows, updated, status);
        PositionFlyout();
        flyout.Show();
    }

    private void PositionFlyout()
    {
        var area = Screen.FromRectangle(Bounds).WorkingArea; var size = flyout.Size;
        int x, y;
        if (store.State.Display.MiniVertical)
        {
            x = Right + 8 + size.Width <= area.Right ? Right + 8 : Left - 8 - size.Width; y = Top;
        }
        else
        {
            y = Bottom + 8 + size.Height <= area.Bottom ? Bottom + 8 : Top - 8 - size.Height; x = Left;
        }
        flyout.Location = new Point(Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - size.Width)), Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
        flyout.TopMost = TopMost;
    }

    private System.Windows.Controls.ContextMenu BuildMenu()
    {
        var display = store.State.Display;
        var menu = new System.Windows.Controls.ContextMenu();
        MiniParts.Dark(menu);
        menu.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Raised"); menu.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Foreground");
        System.Windows.Controls.MenuItem Item(string title, Action action, bool? check = null, string? gesture = null)
        {
            var item = new System.Windows.Controls.MenuItem { Header = title, IsCheckable = check.HasValue, IsChecked = check ?? false, InputGestureText = gesture };
            item.Click += (_, _) => action(); return item;
        }
        menu.Items.Add(Item("Always on top", () => PreferenceChanged?.Invoke(d => d.MiniOnTop = !display.MiniOnTop), display.MiniOnTop));
        menu.Items.Add(Item(display.MiniCollapsed ? "Expand widget" : "Collapse to ball", ToggleCollapsed));
        menu.Items.Add(Item("Lock position", () => PreferenceChanged?.Invoke(d => d.MiniLocked = !display.MiniLocked), display.MiniLocked));
        menu.Items.Add(Item("Vertical layout", () => PreferenceChanged?.Invoke(d => d.MiniVertical = !display.MiniVertical), display.MiniVertical));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(Item("Open OpenUsage", () => OpenRequested?.Invoke()));
        menu.Items.Add(Item("Refresh now", () => RefreshRequested?.Invoke()));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(Item("Hide widget", () => HideRequested?.Invoke()));
        menu.Opened += (_, _) => flyout.Hide();
        return menu;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged; hover.Dispose(); leave.Dispose(); flyout.Dispose(); }
        base.Dispose(disposing);
    }

    // The hover flyout is its own non-activating window so it never takes focus from what the user is doing.
    private sealed class FlyoutForm : Form
    {
        private readonly AccountStore store;
        private readonly MiniFlyoutView content = new();
        public event Action? OpenRequested;
        public event Action? ContentChanged;
        public FlyoutForm(AccountStore store)
        {
            this.store = store;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(0x20, 0x20, 0x20);
            Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = content, BackColor = BackColor });
            content.OpenRequested += () => OpenRequested?.Invoke();
            content.ContentChanged += () => { FitContent(); ContentChanged?.Invoke(); };
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080; return p; } }
        public void Render(IReadOnlyList<MiniRow> rows, DateTimeOffset? updated, string status)
        {
            content.Render(store, rows, updated, status);
            FitContent();
        }
        private void FitContent()
        {
            content.MaxHeight = (Screen.FromPoint(Cursor.Position).WorkingArea.Height - 16) / (DeviceDpi / 96d);
            content.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var scale = DeviceDpi / 96d; ClientSize = new Size((int)Math.Ceiling(content.DesiredSize.Width * scale), (int)Math.Ceiling(content.DesiredSize.Height * scale));
            using var path = RoundedPath(new Rectangle(0, 0, Width, Height), (int)(8 * scale)); Region = new Region(path);
        }
    }
}
