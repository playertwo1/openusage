namespace OpenUsage.Windows;

public sealed record PaceDay(DateTimeOffset Start, double? Used);

// Pace of the current weekly window, from the provider's own percentage and reset time (window = 168h).
public sealed record WeeklyPace(DateTimeOffset Start, double Used, double ElapsedHours, double Expected, double Delta, double Projection, double? RunOutHours, double PerDayLeft, IReadOnlyList<PaceDay> Days, IReadOnlyList<(double Day, double Percent)> Points)
{
    public const double WindowHours = 168;
    public bool RunsOut => RunOutHours is not null;
    public string Verdict => RunOutHours is double hours ? "Runs out " + Start.AddHours(hours).ToLocalTime().ToString("ddd", System.Globalization.CultureInfo.InvariantCulture) : "Lasts week";
    public string Trend => Math.Abs(Delta) <= 5 ? "on pace" : Delta > 0 ? $"+{Delta:0}%" : $"{Delta:0}%";

    public static WeeklyPace? From(Metric weekly, IReadOnlyList<WeekSample> samples, DateTimeOffset now)
    {
        if (weekly.ResetsAt is not DateTimeOffset reset || weekly.Percent is not double used) return null;
        var start = reset.AddHours(-WindowHours);
        var left = Math.Max(0, (reset - now).TotalHours);
        var elapsed = Math.Clamp(WindowHours - left, 0, WindowHours);
        var expected = elapsed / WindowHours * 100;
        var rate = elapsed >= 1 ? used / elapsed : 0;
        var projection = elapsed >= 1 ? rate * WindowHours : used;
        double? runOut = projection > 100 && rate > 0 ? 100 / rate : null;
        var week = samples.Where(s => s.At >= start && s.At <= now).OrderBy(s => s.At).ToArray();
        double? EndOfDay(int i) { var end = start.AddDays(i + 1); return week.LastOrDefault(s => s.At < end)?.Percent; }
        var days = new List<PaceDay>();
        for (var i = 0; i < 7; i++)
        {
            double? spent = null;
            if (start.AddDays(i) <= now && EndOfDay(i) is double end && (i == 0 ? 0 : EndOfDay(i - 1)) is double before) spent = Math.Max(0, end - before);
            days.Add(new PaceDay(start.AddDays(i), spent));
        }
        var points = new List<(double, double)> { (0, 0) };
        points.AddRange(week.Select(s => ((s.At - start).TotalDays, s.Percent)));
        points.Add((elapsed / 24, used));
        return new WeeklyPace(start, used, elapsed, expected, used - expected, projection, runOut, Math.Max(0, 100 - used) / Math.Max(left / 24, 1d / 24), days, points);
    }
}
