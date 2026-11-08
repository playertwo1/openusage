namespace OpenUsage.Windows;

// Strongest first: an account is ranked by its most urgent visible metric.
public enum PriorityReason { Critical, Warning, BurningFast, UseItOrLoseIt }

// Overview Priority rules. Eligibility uses provider-reported values only and never estimates an unknown window.
public static class QuotaPriority
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(5);
    private const double MinimumElapsedHours = 1.68; // one percent of the 168-hour week

    // Burning Fast: a known weekly window, a fresh snapshot, at least 5% used, and a projected run-out before the reset.
    public static bool BurningFast(Metric metric, IReadOnlyList<WeekSample> samples, DateTimeOffset? fetchedAt, DateTimeOffset now)
    {
        if (metric.WindowHours != WeeklyPace.WindowHours || fetchedAt is not DateTimeOffset fetched || now - fetched > FreshFor) return false;
        if (metric.Percent is not double used || used < 5) return false;
        if (WeeklyPace.From(metric, samples, now) is not { } pace || pace.ElapsedHours < MinimumElapsedHours) return false;
        return pace.RunOutHours is double runOut && runOut < WeeklyPace.WindowHours;
    }

    public static PriorityReason? Reason(Metric metric, bool burningFast, DisplayPreferences display, DateTimeOffset now) => MetricPresentation.Severity(metric, display) switch
    {
        QuotaSeverity.Critical => PriorityReason.Critical,
        QuotaSeverity.Warning => PriorityReason.Warning,
        _ => burningFast ? PriorityReason.BurningFast : MetricPresentation.IsUnusedNearReset(metric, now) ? PriorityReason.UseItOrLoseIt : null,
    };

    // The account's most urgent visible metric; ties within a reason go to the sooner event (reset or projected run-out).
    public static (Metric Metric, PriorityReason Reason)? Strongest(AccountStore store, Account account, AccountStatus? status, DateTimeOffset now)
    {
        if (status?.Snapshot is not UsageSnapshot snapshot) return null;
        var samples = store.State.History.GetValueOrDefault(account.Id) ?? [];
        var candidates = new List<(Metric Metric, PriorityReason Reason, DateTimeOffset At)>();
        foreach (var metric in snapshot.Metrics.Where(m => !store.IsHidden(account, m)))
        {
            var burning = BurningFast(metric, samples, status.FetchedAt, now);
            if (Reason(metric, burning, store.State.Display, now) is not { } reason) continue;
            var at = metric.ResetsAt ?? DateTimeOffset.MaxValue;
            if (reason == PriorityReason.BurningFast && WeeklyPace.From(metric, samples, now) is { RunOutHours: double runOut } pace) at = pace.Start.AddHours(runOut);
            candidates.Add((metric, reason, at));
        }
        return candidates.Count == 0 ? null : candidates.OrderBy(c => c.Reason).ThenBy(c => c.At).Select(c => (c.Metric, c.Reason)).First();
    }

    public static string Describe(PriorityReason reason) => reason switch
    {
        PriorityReason.Critical => "Critical",
        PriorityReason.Warning => "Warning",
        PriorityReason.BurningFast => "Burning fast",
        _ => "Use it or lose it",
    };
}
