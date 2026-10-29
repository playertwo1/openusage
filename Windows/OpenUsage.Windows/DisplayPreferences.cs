using System.Globalization;

namespace OpenUsage.Windows;

public enum ColorTheme { System, Light, Dark }
public enum ViewDensity { Comfortable, Compact }
public enum DashboardLayout { Cards, List }
public enum UsageDisplay { Used, Remaining }
public enum ResetDisplay { Countdown, Exact }

public sealed class DisplayPreferences
{
    public ColorTheme Theme { get; set; } = ColorTheme.System;
    public ViewDensity Density { get; set; } = ViewDensity.Comfortable;
    public DashboardLayout Layout { get; set; } = DashboardLayout.Cards;
    public UsageDisplay Usage { get; set; } = UsageDisplay.Used;
    public ResetDisplay Reset { get; set; } = ResetDisplay.Countdown;
    public bool FavoritesOnly { get; set; }
    public bool HideAccountNames { get; set; }
    public int TextSize { get; set; } = 100;
    public bool MiniWidget { get; set; }
    public bool MiniVertical { get; set; }
    public bool MiniOnTop { get; set; } = true;
    public bool MiniLocked { get; set; }
    public string? MiniMonitor { get; set; }
    // Position inside the monitor's work area, 0 to 1 on each axis; negative means the default corner.
    public double MiniX { get; set; } = -1;
    public double MiniY { get; set; } = -1;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Enum.IsDefined(Theme) && Enum.IsDefined(Density) && Enum.IsDefined(Layout)
        && Enum.IsDefined(Usage) && Enum.IsDefined(Reset) && TextSize is 100 or 110 or 125;
}

public static class MetricPresentation
{
    // Fixed identity colour per window; use only changes the number, never the bar or ring.
    public static string BaseTone(string label) => label switch { "Session" => "S5h", "Weekly" => "Swk", _ => "Accent" };
    public static string AccountName(Account account, IReadOnlyList<Account> accounts, bool hide)
    {
        if (!hide) return account.Label;
        var ordinal = 0;
        foreach (var current in accounts.Where(a => a.Kind == account.Kind))
        {
            ordinal++;
            if (current.Id == account.Id) return $"Account {ordinal}";
        }
        return "Account";
    }

    public static double? Fill(Metric metric, DisplayPreferences display) => metric.Percent is double used
        ? display.Usage == UsageDisplay.Remaining ? 100 - used : used : null;

    public static string Value(Metric metric, DisplayPreferences display)
    {
        if (metric.Text is not null) return metric.Text;
        if (metric.Unit == "percent")
        {
            var value = Math.Clamp(metric.Value, 0, 100);
            return display.Usage == UsageDisplay.Remaining ? $"{100 - value:0.#}% left" : $"{value:0.#}% used";
        }
        if (display.Usage == UsageDisplay.Remaining && metric.Limit is double limit)
        {
            var remaining = Math.Max(0, limit - metric.Value);
            return metric.Unit == "usd" ? $"${remaining:0.00} left" : $"{remaining:0.##} {metric.Unit} left";
        }
        return metric.Display;
    }

    public static string Reset(DateTimeOffset? reset, DisplayPreferences display, DateTimeOffset now)
    {
        if (reset is null) return "";
        if (display.Reset == ResetDisplay.Exact) return reset.Value.ToLocalTime().ToString("ddd, dd MMM · HH:mm", CultureInfo.CurrentCulture);
        var time = reset.Value - now;
        if (time <= TimeSpan.Zero) return "Reset due";
        return time.TotalDays >= 1 ? $"Resets in {(int)time.TotalDays}d {time.Hours}h"
            : time.TotalHours >= 1 ? $"Resets in {(int)time.TotalHours}h {time.Minutes}m" : $"Resets in {Math.Max(1, time.Minutes)}m";
    }
}
