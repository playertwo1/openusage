using System.Security.Cryptography;
using System.Text.Json;

namespace OpenUsage.Windows;

// Quota severity, Priority eligibility, persisted alert switches and alert de-duplication. No UI.
public static class QuotaPriorityChecks
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    public static void Run(string directory)
    {
        Defaults(directory);
        Thresholds();
        Severity();
        UseItOrLoseIt();
        BurningFast();
        AlertSequences(directory);
    }

    private static void Defaults(string directory)
    {
        var fresh = new DisplayPreferences();
        Check(fresh.PriorityWarningLeft == 30 && fresh.PriorityCriticalLeft == 10 && !fresh.PriorityOnly && fresh.IsValid, "Priority Defaults Must Be Warning 30, Critical 10, Filter Off");
        var account = new Account(Guid.NewGuid(), ProviderKind.Claude, "Legacy", new Credential("fixture-only"));
        var legacy = JsonSerializer.SerializeToUtf8Bytes(new { Schema = 1, Accounts = new[] { account }, Notifications = true });
        Directory.CreateDirectory(Path.Combine(directory, "legacy"));
        File.WriteAllBytes(Path.Combine(directory, "legacy", "accounts.dat"), ProtectedData.Protect(legacy, null, DataProtectionScope.CurrentUser));
        var upgraded = new AccountStore(Path.Combine(directory, "legacy"));
        Check(upgraded.State.Notifications && !upgraded.State.WarningNotifications && !upgraded.State.UseItOrLoseItNotifications, "Legacy Low Usage Preference Must Stay The Critical Alert, New Switches Off");
        Check(upgraded.State.Display.PriorityWarningLeft == 30 && upgraded.State.Display.PriorityCriticalLeft == 10 && !upgraded.State.Display.PriorityOnly, "Legacy Files Must Gain Priority Defaults");
        var removedProvider = new Account(Guid.NewGuid(), (ProviderKind)9, "Retired", new Credential("fixture-only"));
        var mixed = JsonSerializer.SerializeToUtf8Bytes(new { Schema = 1, Accounts = new[] { removedProvider, account }, Notifications = false });
        Directory.CreateDirectory(Path.Combine(directory, "retired"));
        File.WriteAllBytes(Path.Combine(directory, "retired", "accounts.dat"), ProtectedData.Protect(mixed, null, DataProtectionScope.CurrentUser));
        var kept = new AccountStore(Path.Combine(directory, "retired")).State.Accounts;
        Check(kept.Count == 1 && kept[0].Id == account.Id, "A Removed Provider's Saved Account Must Be Dropped While The Rest Of The File Loads");
        var store = new AccountStore(Path.Combine(directory, "persist"));
        store.Edit(s => { s.WarningNotifications = true; s.UseItOrLoseItNotifications = true; s.Display.SetPriorityWarningLeft(25); s.Display.PriorityOnly = true; });
        var reloaded = new AccountStore(Path.Combine(directory, "persist")).State;
        Check(reloaded.WarningNotifications && reloaded.UseItOrLoseItNotifications && reloaded.Display.PriorityWarningLeft == 25 && reloaded.Display.PriorityOnly, "Priority Preferences Must Persist");
    }

    private static void Thresholds()
    {
        Check(DisplayPreferences.ValidWarning(20) && DisplayPreferences.ValidWarning(70) && !DisplayPreferences.ValidWarning(19) && !DisplayPreferences.ValidWarning(72), "Warning Range Is 20 To 70 In Steps Of 5");
        Check(DisplayPreferences.ValidCritical(5) && DisplayPreferences.ValidCritical(25) && !DisplayPreferences.ValidCritical(4) && !DisplayPreferences.ValidCritical(12), "Critical Range Is 5 To 25 In Steps Of 5");
        Check(DisplayPreferences.ValidThresholds(30, 10) && DisplayPreferences.ValidThresholds(25, 20) && !DisplayPreferences.ValidThresholds(20, 20), "Critical Must Stay At Least Five Below Warning");
        var pair = new DisplayPreferences();
        pair.SetPriorityCriticalLeft(20);
        Check(pair.PriorityCriticalLeft == 20 && pair.PriorityWarningLeft == 30, "Setting Critical Below Warning Must Leave Warning Alone");
        pair.SetPriorityWarningLeft(20);
        Check(pair.PriorityWarningLeft == 20 && pair.PriorityCriticalLeft == 15, "Warning Set At Or Below Critical Must Lower Critical To Warning Minus Five");
        pair.SetPriorityCriticalLeft(20);
        Check(pair.PriorityWarningLeft == 25 && pair.PriorityCriticalLeft == 20 && pair.IsValid, "Critical Set At Or Above Warning Must Raise Warning To Critical Plus Five");
        Rejects(() => pair.SetPriorityWarningLeft(75), "Out-Of-Range Warning Must Be Refused");
        Rejects(() => pair.SetPriorityCriticalLeft(12), "Non-Step Critical Must Be Refused");
        Check(!new DisplayPreferences { PriorityWarningLeft = 20, PriorityCriticalLeft = 20 }.IsValid, "An Invalid Saved Threshold Pair Must Fail Validation");
    }

    private static void Severity()
    {
        var display = new DisplayPreferences();
        Check(MetricPresentation.Severity(new Metric("Session", 70), display) == QuotaSeverity.Warning, "Exactly 30 Percent Left Must Be Warning");
        Check(MetricPresentation.Severity(new Metric("Session", 69.9), display) == QuotaSeverity.Normal, "Above 30 Percent Left Must Be Normal");
        Check(MetricPresentation.Severity(new Metric("Session", 90), display) == QuotaSeverity.Critical, "Exactly 10 Percent Left Must Be Critical");
        Check(MetricPresentation.Severity(new Metric("Session", 89.9), display) == QuotaSeverity.Warning, "Just Above 10 Percent Left Must Be Warning");
        Check(MetricPresentation.Severity(new Metric("Balance", 12, Unit: "usd"), display) == QuotaSeverity.Normal, "Unbounded Balances Must Not Be Severe");
        display.Usage = UsageDisplay.Remaining;
        Check(MetricPresentation.Severity(new Metric("Session", 90), display) == QuotaSeverity.Critical, "Remaining Display Must Not Change Severity");
        Check(MetricPresentation.Severity(new Metric("Cost", 95, 100, "usd"), display) == QuotaSeverity.Critical, "A Bounded Dollar Limit Uses Its Percentage");
    }

    private static void UseItOrLoseIt()
    {
        var reset = Now.AddHours(24);
        Check(MetricPresentation.IsUnusedNearReset(new Metric("Weekly", 40, ResetsAt: reset, WindowHours: 168), Now), "Exactly 24 Hours Before Reset With 60 Percent Left Must Qualify");
        Check(!MetricPresentation.IsUnusedNearReset(new Metric("Weekly", 40, ResetsAt: reset.AddSeconds(1), WindowHours: 168), Now), "More Than 24 Hours Before Reset Must Not Qualify");
        Check(!MetricPresentation.IsUnusedNearReset(new Metric("Weekly", 40, ResetsAt: Now, WindowHours: 168), Now), "A Reset Exactly Now Must Not Qualify");
        Check(!MetricPresentation.IsUnusedNearReset(new Metric("Weekly", 50, ResetsAt: reset, WindowHours: 168), Now), "Exactly 50 Percent Left Must Not Qualify");
        Check(MetricPresentation.IsUnusedNearReset(new Metric("Weekly", 49.9, ResetsAt: reset, WindowHours: 168), Now), "Just Over 50 Percent Left Must Qualify");
        Check(!MetricPresentation.IsUnusedNearReset(new Metric("Session", 10, ResetsAt: reset, WindowHours: 5), Now), "Session Windows Must Never Qualify");
        Check(!MetricPresentation.IsUnusedNearReset(new Metric("Weekly", 10, ResetsAt: reset), Now), "Unknown Windows Must Never Qualify");
        Check(!MetricPresentation.IsUnusedNearReset(new Metric("Cost", 10, 100, "usd", reset, WindowHours: 168), Now), "Dollar Caps Must Never Qualify");
        Check(!MetricPresentation.IsUnusedNearReset(new Metric("Weekly", 10, WindowHours: 168), Now), "A Missing Reset Must Never Qualify");
    }

    private static void BurningFast()
    {
        var fresh = Now;
        var at48 = Now.AddHours(120);
        var weekly = new Metric("Weekly", 40, ResetsAt: at48, WindowHours: 168);
        Check(QuotaPriority.BurningFast(weekly, [], fresh, Now), "Fast Weekly Use With A Fresh Snapshot Must Qualify");
        Check(QuotaPriority.BurningFast(weekly, [], Now.AddMinutes(-5), Now), "A Snapshot Exactly Five Minutes Old Still Qualifies");
        Check(!QuotaPriority.BurningFast(weekly, [], Now.AddMinutes(-6), Now), "A Stale Snapshot Must Not Qualify");
        Check(!QuotaPriority.BurningFast(new Metric("Weekly", 4, ResetsAt: at48, WindowHours: 168), [], fresh, Now), "Under Five Percent Used Must Not Qualify");
        Check(!QuotaPriority.BurningFast(new Metric("Weekly", 30, ResetsAt: Now.AddHours(167), WindowHours: 168), [], fresh, Now), "Under 1.68 Elapsed Hours Must Not Qualify");
        Check(!QuotaPriority.BurningFast(new Metric("Weekly", 10, ResetsAt: at48, WindowHours: 168), [], fresh, Now), "A Projection That Lasts The Week Must Not Qualify");
        Check(!QuotaPriority.BurningFast(new Metric("Weekly", 40, ResetsAt: at48), [], fresh, Now), "An Unknown Weekly Window Must Not Qualify");
        Check(!QuotaPriority.BurningFast(new Metric("Weekly", 40, WindowHours: 168), [], fresh, Now), "A Missing Reset Must Not Qualify");
    }

    private static void AlertSequences(string directory)
    {
        var store = new AccountStore(Path.Combine(directory, "alerts"));
        var account = new Account(Guid.NewGuid(), ProviderKind.Codex, "Alerts", new Credential("fixture-only"));
        store.Add(account);
        var evaluator = new UsageAlertEvaluator();
        IReadOnlyList<UsageAlert> Observe(double used, DateTimeOffset? reset = null, int windowHours = 5) => evaluator.Evaluate(store, account,
            new UsageSnapshot("Plus", [new Metric("Session", used, ResetsAt: reset ?? Now.AddHours(4), WindowHours: windowHours)]), Now);

        Check(Observe(50).Count == 0, "Normal Usage Must Not Alert");
        store.Edit(s => s.Notifications = true);
        Check(Observe(75).Count == 0, "Warning Must Stay Silent While Its Switch Is Off");
        Check(Observe(75).Count == 0, "Repeated Warning Must Not Repeat");
        var critical = Observe(92);
        Check(critical.Count == 1 && critical[0].Kind == AlertKind.Critical, "Entering Critical Must Send One Critical Alert");
        Check(Observe(95).Count == 0, "Repeated Critical Refreshes Must Not Repeat");
        Check(Observe(60).Count == 0 && Observe(91).Count == 1, "Recovering Above Critical Must Re-arm Critical");

        store.Edit(s => s.WarningNotifications = true);
        evaluator = new UsageAlertEvaluator();
        Check(Observe(50).Count == 0, "Normal Usage Must Stay Silent With Warning On");
        var warning = Observe(75);
        Check(warning.Count == 1 && warning[0].Kind == AlertKind.Warning, "Entering Warning Must Send One Warning Alert");
        Check(Observe(75).Count == 0, "Repeated Warning Must Be Silent");
        var escalated = Observe(92);
        Check(escalated.Count == 1 && escalated[0].Kind == AlertKind.Critical, "Escalating From Warning Must Send Only The Critical Alert");
        evaluator = new UsageAlertEvaluator();
        var jump = Observe(92);
        Check(jump.Count == 1 && jump[0].Kind == AlertKind.Critical, "A Direct Jump To Critical Must Send One Alert, Not Two");

        store.Edit(s => { s.Display.Usage = UsageDisplay.Remaining; s.Hidden.Add(AccountStore.MetricKey(account.Id, "Session")); });
        evaluator = new UsageAlertEvaluator();
        Check(Observe(95).Count == 0, "A Hidden Metric Must Not Alert");
        store.Edit(s => s.Hidden.Clear());

        store.Edit(s => s.UseItOrLoseItNotifications = false);
        evaluator = new UsageAlertEvaluator();
        var weekReset = Now.AddHours(12);
        IReadOnlyList<UsageAlert> Week(double used, DateTimeOffset reset) => evaluator.Evaluate(store, account,
            new UsageSnapshot("Plus", [new Metric("Weekly", used, ResetsAt: reset, WindowHours: 168)]), Now);
        Check(Week(20, weekReset).Count == 0, "The First Observation After Launch Must Prime Without Notifying");
        store.Edit(s => s.UseItOrLoseItNotifications = true);
        Check(Week(20, weekReset).Count == 0, "A Primed Cycle Must Not Notify Later");
        var changed = Week(20, Now.AddHours(20));
        Check(changed.Count == 1 && changed[0].Kind == AlertKind.UseItOrLoseIt, "A Changed Reset Inside The Window Must Re-arm Use It Or Lose It");
        Check(Week(20, Now.AddHours(20)).Count == 0, "The Re-armed Cycle Must Notify Once");
        evaluator = new UsageAlertEvaluator();
        Check(Week(20, Now.AddHours(48)).Count == 0, "A Cycle Outside The Window Must Not Notify");
        var entering = Week(20, Now.AddHours(10));
        Check(entering.Count == 1 && entering[0].Kind == AlertKind.UseItOrLoseIt, "Entering The 24-Hour Window Must Notify Once");
        Check(Week(20, Now.AddHours(10)).Count == 0, "Repeated Use It Refreshes Must Not Repeat");
        var renewed = Week(20, Now.AddHours(22));
        Check(renewed.Count == 1 && renewed[0].Kind == AlertKind.UseItOrLoseIt, "A Changed Reset Timestamp Must Re-arm Use It Or Lose It");
    }

    private static void Rejects(Action action, string message)
    {
        try { action(); } catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("QuotaPriority: " + message);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception("QuotaPriority: " + message); }
}
