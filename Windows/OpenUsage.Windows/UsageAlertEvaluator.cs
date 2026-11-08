namespace OpenUsage.Windows;

public enum AlertKind { Critical, Warning, UseItOrLoseIt }

public sealed record UsageAlert(Guid AccountId, Metric Metric, AlertKind Kind);

// Turns each refresh into the alerts it newly earns. State is in memory: a relaunch primes it again, so no alert repeats across refreshes.
public sealed class UsageAlertEvaluator
{
    private readonly Dictionary<string, QuotaSeverity> severities = [];
    private readonly HashSet<string> observed = [];
    private readonly Dictionary<string, DateTimeOffset> handledResets = [];

    public IReadOnlyList<UsageAlert> Evaluate(AccountStore store, Account account, UsageSnapshot snapshot, DateTimeOffset now)
    {
        var alerts = new List<UsageAlert>();
        var state = store.State;
        foreach (var metric in snapshot.Metrics)
        {
            if (store.IsHidden(account, metric)) continue;
            var key = AccountStore.MetricKey(account.Id, metric.Label);
            var firstSight = observed.Add(key);

            // Critical and Warning fire when a refresh enters a more urgent level than the last one seen.
            var severity = MetricPresentation.Severity(metric, state.Display);
            var previous = severities.GetValueOrDefault(key, QuotaSeverity.Normal);
            severities[key] = severity;
            if (severity > previous)
            {
                var kind = severity == QuotaSeverity.Critical ? AlertKind.Critical : AlertKind.Warning;
                if (kind == AlertKind.Critical ? state.Notifications : state.WarningNotifications) alerts.Add(new(account.Id, metric, kind));
            }

            // Use It or Lose It: the first sight of a qualifying cycle only records it; a later new cycle in the window notifies once.
            if (!MetricPresentation.IsUnusedNearReset(metric, now) || metric.ResetsAt is not DateTimeOffset reset) continue;
            if (handledResets.GetValueOrDefault(key) == reset) continue;
            handledResets[key] = reset;
            if (!firstSight && state.UseItOrLoseItNotifications) alerts.Add(new(account.Id, metric, AlertKind.UseItOrLoseIt));
        }
        return alerts;
    }
}
