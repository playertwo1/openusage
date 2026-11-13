# Personal Quota Priorities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give personal Windows users consistent quota severity, a useful Priority filter, and an optional warning before eligible quota expires mostly unused.

**Architecture:** Store threshold and filter choices in the existing `DisplayPreferences`, preserve the current Critical alert preference, and centralize severity, window eligibility and alert-cycle rules in testable Windows helpers. Reuse `WeeklyPace` for weekly projections.

**Tech Stack:** C# 14, .NET 10, WPF/Windows Forms, the existing Windows console check harness.

**Spec:** [Roadmap, Stage 2](../../roadmap.md#stage-2-make-quota-urgency-consistent-and-configurable); upstream [priority levels](https://github.com/robinebers/openusage/pull/1588), [Burning Fast](https://github.com/robinebers/openusage/pull/1594), [Use It or Lose It alert](https://github.com/robinebers/openusage/pull/1598), and [Priority section](https://github.com/robinebers/openusage/pull/1603).

## Global Constraints

- Change only the Windows edition and Windows documentation.
- Defaults: Warning at 30% remaining and Critical at 10% remaining. Warning range 20–70 by 5; Critical range 5–25 by 5; Critical stays at least 5 below Warning.
- Preserve `AppState.Notifications` as the saved Critical-alert preference. New Warning and Use It or Lose It alerts default off.
- Use provider-reported quota values and reset times only. Never estimate cost, reset time or a provider's unreported window.
- Keep provider/account order as saved; hidden metrics remain hidden; existing Favorites behavior intersects with Priority when both filters are active.
- Start by checking `git status --short`; preserve existing edits and isolate work if target files are already modified.

## Review Focus

- Threshold equality: exactly 30% left is Warning; exactly 10% left is Critical; changing Used/Remaining display does not change severity.
- Invalid saved threshold pairs, legacy files without new fields, and a user moving one threshold past the other.
- No reset, unknown/daily/session window, balance, dollar cap, stale snapshot, or missing weekly history: no false Use It or Lose It/Burning Fast signal.
- Reset-cycle rollover and repeated refresh: new alerts fire at most once per metric per reset cycle; a new reset re-arms them.
- Hidden/favorite filters, account-name privacy, and saved account/provider order remain correct in Overview Priority.

---

### Task 1: Add backward-compatible priority preferences and settings

**Files:**
- Modify: `Windows/OpenUsage.Windows/DisplayPreferences.cs`
- Modify: `Windows/OpenUsage.Windows/AccountStore.cs`
- Modify: `Windows/OpenUsage.Windows/ShellView.Settings.cs`
- Modify: `Windows/OpenUsage.Windows.Tests/Program.cs`
- Test: `Windows/OpenUsage.Windows.Tests/UiChecks.cs`

**Interfaces:**
- Produces: `DisplayPreferences.PriorityWarningLeft` (default 30), `PriorityCriticalLeft` (default 10), and `PriorityOnly` (default false).
- Produces: `AppState.WarningNotifications` and `AppState.UseItOrLoseItNotifications`, both default false. Existing `AppState.Notifications` continues to mean Critical alerts.
- Produces: `SetPriorityWarningLeft(int)` and `SetPriorityCriticalLeft(int)`, enforcing the specified 5-point gap.

- [x] **Step 1: Add failing storage and UI checks** for defaults, legacy account-file loading, threshold ranges, both threshold push rules, invalid values, new alert switches, and a selected Priority filter.
- [ ] **Step 2: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new persistence and UI assertions.
- [x] **Step 3: Add the preferences and settings controls.** Warning choices are 20, 25, …, 70; Critical choices are 5, 10, …, 25. When Warning is set at or below Critical, lower Critical to Warning − 5; when Critical is set at or above Warning, raise Warning to Critical + 5. Validate the saved pair in `DisplayPreferences.IsValid`.
- [x] **Step 4: Keep existing account data compatible.** Missing JSON fields take the defaults above; do not change `AppState.Schema`, account IDs, metric labels or credential fields. Keep the current notification toggle's stored value as Critical and add separate off-by-default Warning and Use It or Lose It toggles.
- [x] **Step 5: Run `./script/windows.ps1 test`.**
  **Expected:** PASS, including legacy encrypted account-file round-trip checks.
- [ ] **Step 6: Commit the task** after checking the staged diff contains only intended files.

### Task 2: Add explicit window metadata and shared quota rules

**Files:**
- Modify: `Windows/OpenUsage.Windows/Models.cs`
- Modify: `Windows/OpenUsage.Windows/UsageMappers.cs`
- Modify: `Windows/OpenUsage.Windows/UsageMappers.Antigravity.cs`
- Modify: `Windows/OpenUsage.Windows/DisplayPreferences.cs`
- Create: `Windows/OpenUsage.Windows/QuotaPriority.cs`
- Create: `Windows/OpenUsage.Windows.Tests/QuotaPriorityChecks.cs`
- Modify: `Windows/OpenUsage.Windows.Tests/UsageChecks.cs`
- Modify: `Windows/OpenUsage.Windows.Tests/Program.cs`

**Interfaces:**
- Consumes: currency fields appended by [Stage 1](2026-11-08-claude-billing-currency-windows.md), when that stage is implemented first.
- Produces: `Metric.WindowHours` as an optional field appended after currency fields.
- Produces: `MetricPresentation.Severity(Metric, DisplayPreferences) -> QuotaSeverity`.
- Produces: `MetricPresentation.IsUnusedNearReset(Metric, DateTimeOffset now) -> bool`.

- [x] **Step 1: Add failing rule and mapper checks** for Warning/Critical boundaries, Used/Remaining independence, and Use It or Lose It at exactly/inside/outside 24 hours and 50% left.
- [ ] **Step 2: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new rule and window-metadata assertions.
- [x] **Step 3: Append `double? WindowHours = null` to `Metric`.** Populate only known provider windows: Claude Session 5 and weekly metrics 168; Codex only when `limit_window_seconds` is 18,000 or 604,800; Antigravity 5-hour and weekly bucket IDs; Grok from provider `start`/`end`; Ollama Session 5 and Weekly 168; OpenCode rolling 5, Weekly 168 and Monthly 720; Z.ai from its parsed unit × count. Leave positional Codex fallbacks and Copilot/Cursor windows null unless the response explicitly supplies a known period.
- [x] **Step 4: Implement `QuotaSeverity` and `MetricPresentation.Severity`.** Use `Metric.Percent` and remaining percentage, with Critical checked before Warning. A missing percentage is Normal.
- [x] **Step 5: Implement `MetricPresentation.IsUnusedNearReset`.** Return true only for `Unit == "percent"`, a known `WindowHours > 24`, `ResetsAt > now`, `ResetsAt <= now + 24 hours`, and `100 - Percent > 50`. This excludes dollar caps, balances, sessions, daily and unknown windows.
- [x] **Step 6: Run `./script/windows.ps1 test`.**
  **Expected:** PASS for all mapper and rule boundary checks.
- [ ] **Step 7: Commit the task** after checking the staged diff contains only intended files.

### Task 3: Apply severity to every Windows surface and add Priority filtering

**Files:**
- Modify: `Windows/OpenUsage.Windows/DashboardView.Rings.cs`
- Modify: `Windows/OpenUsage.Windows/DashboardView.Manage.cs`
- Modify: `Windows/OpenUsage.Windows/MiniWidgetView.cs`
- Modify: `Windows/OpenUsage.Windows/ShellView.cs`
- Modify: `Windows/OpenUsage.Windows/DashboardView.cs`
- Test: `Windows/OpenUsage.Windows.Tests/UiChecks.cs`
- Test: `Windows/OpenUsage.Windows.Tests/QuotaPriorityChecks.cs`

**Interfaces:**
- Consumes: `MetricPresentation.Severity`, `IsUnusedNearReset`, `Metric.WindowHours`, and `DisplayPreferences.PriorityOnly`.
- Produces: Overview filtering that displays each eligible account once, in saved provider/account order, with a subtitle naming its most urgent qualifying metric and reason.

- [x] **Step 1: Add failing UI checks** for the Priority button, an eligible and ineligible account, a hidden qualifying metric, Favorites ∩ Priority, anonymous account names, and preserved provider/account order.
- [ ] **Step 2: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new UI assertions.
- [x] **Step 3: Replace fixed 85%-used visual checks** in Overview, Accounts and the mini widget with `MetricPresentation.Severity`. Keep mini-widget alert dots absent.
- [x] **Step 4: Add the Priority button beside All/Favorites.** Make `PriorityOnly` an independent persisted filter. Include an account once if any non-hidden metric is Critical, Warning, Burning Fast or Use It or Lose It. Preserve saved account order. When filters overlap, require both conditions.
- [x] **Step 5: Identify the strongest reason in this order:** Critical, Warning, Burning Fast, Use It or Lose It. For equal reasons, choose the metric with the sooner relevant event. Use `MetricPresentation.AccountName` for the subtitle so account-name privacy still applies.
- [x] **Step 6: Define Burning Fast from `WeeklyPace.From`.** Require a fresh account snapshot (`FetchedAt` no older than five minutes), a known weekly metric, at least 5% used, at least 1.68 elapsed hours in the 168-hour window, and `RunOutHours < WeeklyPace.WindowHours`. A stale/error snapshot or missing reset is not eligible.
- [x] **Step 7: Run `./script/windows.ps1 test`.**
  **Expected:** PASS for rendered UI, filters, order and eligibility.
- [ ] **Step 8: Commit the task** after checking the staged diff contains only intended files.

### Task 4: Add warning and Use It or Lose It notifications

**Files:**
- Modify: `Windows/OpenUsage.Windows/MainForm.cs`
- Create: `Windows/OpenUsage.Windows/UsageAlertEvaluator.cs`
- Modify: `Windows/OpenUsage.Windows/ShellView.Settings.cs`
- Test: `Windows/OpenUsage.Windows.Tests/QuotaPriorityChecks.cs`
- Test: `Windows/OpenUsage.Windows.Tests/UiChecks.cs`

**Interfaces:**
- Consumes: shared severity/window rules from Task 2 and saved alert preferences from Task 1.
- Produces: a small evaluator used by `MainForm.Notify` to send at most the configured Warning, Critical and Use It or Lose It alerts.

- [x] **Step 1: Add failing evaluator checks** for threshold transitions, repeated refresh, new reset timestamp, Use It cold-start priming, and each off-by-default switch.
- [ ] **Step 2: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new alert assertions.
- [x] **Step 3: Preserve Critical behavior.** Keep `AppState.Notifications` mapped to Critical and keep its current saved default. Add Warning notifications as a separate disabled-by-default choice. Send only the highest newly entered severity for one metric/update; repeated refreshes do not repeat it.
- [x] **Step 4: Add Use It or Lose It notification state.** Key the event by account, metric and reset timestamp. On the first observation after process start, prime a qualifying cycle without notifying; on a later transition into the 24-hour/over-50% window, notify once. A changed reset timestamp starts a new cycle.
- [x] **Step 5: Keep notifications account-specific and private.** Use the existing `MetricPresentation.AccountName` helper and never log provider response bodies or credentials.
- [x] **Step 6: Run `./script/windows.ps1 test`.**
  **Expected:** PASS for boundaries, de-duplication, settings and privacy checks.
- [ ] **Step 7: Commit the task** after checking the staged diff contains only intended files.

### Task 5: Document the Windows behavior

**Files:**
- Modify: `README.md`
- Modify: `docs/windows.md`

- [x] **Step 1: Document** default and configurable severity thresholds, the Priority filter, Burning Fast eligibility, and the optional Use It or Lose It alert.
- [x] **Step 2: Verify** the prose matches Task 2's exact predicates and Task 4's alert defaults.
- [x] **Step 3: Run `./script/windows.ps1 test`.**
  **Expected:** PASS.
- [ ] **Step 4: Commit the task** after checking the staged diff contains only intended files.
