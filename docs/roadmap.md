# OpenUsage Roadmap

This roadmap tracks planned work for the separate Windows development edition in `Windows/`. The
Swift/macOS application and its release line remain independent.

## Next Phase: Personal Usage Signals And Reset Controls

**Status:** Planned
**Scope:** Windows, personal accounts, provider-reported data only.

The upstream releases since this fork introduced several changes that apply to personal accounts:
Claude Extra Usage in the account's billing currency; configurable warning and critical levels;
limits that are burning fast; a warning before a weekly or monthly limit resets mostly unused; and
Z.ai's banked Session and Weekly resets. The Windows app already has weekly pace history, basic low
usage alerts, and Z.ai quota reads, so this phase extends those pieces.

### Explicitly out of scope

- Copilot sign-in through GHE.com, Muse Code, and new Team/Enterprise-only behavior or labels. They
  do not fit the owner's personal-account use.
- macOS-only changes such as SwiftUI/AppKit layout, Sparkle, menu-bar behavior, and automatic
  screen-capture detection.
- Automatic use of Z.ai resets. This phase plans a user-initiated action only, with confirmation.

### Stage 1: Show Claude Extra Usage In Its Billing Currency

1. Read `currency` and `decimal_places` from Claude's `extra_usage` payload.
2. Default an absent or invalid currency code to USD and an absent or invalid decimal-place count
   to 2, independently. Convert provider minor units using the validated decimal-place count instead
   of always dividing by 100.
3. Carry currency metadata through the Windows metric formatter so the Overview, Accounts, mini
   widget, tray summary and Remaining display agree.
4. Add USD, EUR and zero-decimal currency fixtures; update `README.md` and `docs/windows.md`.

**Acceptance:** Existing USD values render unchanged. A non-USD response renders with its ISO currency
code and correct precision in every surface. Invalid optional currency metadata cannot break the
other Claude metrics.

**Implementation plan:** [Claude billing currency](superpowers/plans/2026-11-08-claude-billing-currency-windows.md).

### Stage 2: Make Quota Urgency Consistent And Configurable

1. Add Warning and Critical remaining-percent settings. Defaults: Warning at 30% left, Critical at
   10% left. Keep Warning within 20–70 in 5-point steps and Critical within 5–25 in 5-point steps;
   Critical must remain at least 5 points below Warning.
2. Use the same severity rule in Overview, Accounts, the mini widget and notifications. Preserve the
   current saved low-usage alert preference as the Critical alert preference; add separate opt-in
   Warning and “Use It or Lose It” alert switches, both off by default.
3. Add a Priority filter to Overview. It includes visible account rows with a Warning/Critical
   metric, an eligible Burning Fast weekly metric, or an eligible Use It or Lose It metric. Keep
   provider/account order as saved; keep the existing Favorites filter as an additional filter.
4. Define Burning Fast from the existing `WeeklyPace` projection. Require a current snapshot, at
   least 5% used, at least 1% of the 168-hour window elapsed, and a projected run-out before the
   reset. Do not estimate non-weekly or unclassified limits.
5. Define Use It or Lose It from provider-reported data only: a percentage quota with a known window
   longer than 24 hours, a reset after now and within 24 hours, and more than 50% remaining. Skip
   sessions, daily or unknown windows, missing reset times, balances, and dollar spending caps.
   Reuse this one predicate for the Priority filter and notification.
6. De-duplicate each new alert by account, metric and reset cycle. A changed reset timestamp re-arms
   it. A first observation after app launch primes the Use It or Lose It state without sending a
   notification.
7. Add boundary, stale-data, reset-rollover, privacy and persisted-preference checks; update
   `README.md` and `docs/windows.md`.

**Acceptance:** Threshold colors and alert text agree at exactly 30% and 10% remaining, regardless of
whether values are displayed as Used or Remaining. Priority never invents a reset or applies weekly
pace to an unknown window. Existing account order, hidden metrics, privacy masking and saved alert
preferences continue to work.

**Implementation plan:** [Personal quota priorities](superpowers/plans/2026-11-08-personal-quota-priorities-windows.md).

### Stage 3: Read And Manually Use Z.ai Personal Banked Resets

1. Read Z.ai's `PERSONAL` reset list in the existing hourly secondary-read slot, keyed by the account's
   own API key. Keep Session and Weekly records separate and order each by expiry.
2. Reject malformed or expired records at the provider boundary. A secondary-read failure must leave
   Session, Weekly and Web Searches usable and must not erase the last valid reset list.
3. Show a `Limit Resets` count and a details view with each available record's window and expiry.
   Show a Use action only for the earliest record in that window.
4. Before adding the new metric, get the owner's explicit choices for its tray pin and position in
   the Z.ai metric order. Keep the metric shown by default, in line with project rules.
5. Before enabling the Use action, verify the current Z.ai Personal endpoint and response contract
   from a primary source. The upstream implementation did not test a live Z.ai claim, so its release
   alone is not proof that the `api.z.ai` claim endpoint works for every personal key.
   If that contract cannot be verified, complete only the read-only reset list and leave the Use
   action out; keep this stage pending instead of guessing or sending a live claim.
6. If that gate passes, require a confirmation naming the window, fetch the list again immediately,
   and claim only the exact confirmed earliest record. Send `targetType=PERSONAL`, its exact
   `recordId` and `grantType`, the matching `resetType`, and a fresh request ID. Never auto-use or
   automatically retry an ambiguous POST; refresh and require a new user confirmation.
7. Add mocked list/claim tests for malformed data, duplicate expiries, stale records, failures,
   success and unknown POST outcomes. Update `README.md` and `docs/windows.md`.

**Acceptance:** Ordinary quota refresh succeeds even if the reset-list request fails. No POST can occur
without a confirmation and a successful fresh-list check. A successful claim refreshes the account;
an unknown POST result is never automatically repeated.

**Implementation plan:** [Z.ai banked resets](superpowers/plans/2026-11-08-zai-banked-resets-windows.md).

### Phase completion

- All three stages are complete and the Windows check harness passes with
  `./script/windows.ps1 test`.
- `README.md` and `docs/windows.md` describe the shipped Windows behavior.
- No version number is changed and no release is cut as part of this phase.
- No Swift/macOS files, account credentials, or provider data are changed by the plan itself.

### Upstream basis

- [OpenUsage v0.8.0-beta.2](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.2)
  introduced Z.ai banked resets, configurable priority levels, Burning Fast, Use It or Lose It, and
  the Claude currency fix.
- [Z.ai resets, PR #1576](https://github.com/robinebers/openusage/pull/1576)
- [Priority levels, PR #1588](https://github.com/robinebers/openusage/pull/1588)
- [Burning Fast, PR #1594](https://github.com/robinebers/openusage/pull/1594)
- [Use It or Lose It alert, PR #1598](https://github.com/robinebers/openusage/pull/1598)
- [Use It or Lose It in Priority, PR #1603](https://github.com/robinebers/openusage/pull/1603)
- [Claude billing currency, commit 8a88321](https://github.com/robinebers/openusage/commit/8a88321)
- [Full release comparison for this fork](research/upstream-release-audit-2026-11-08.md)

## Follow-up: Usage History And Manual Reset Use

**Status:** Planned
**Scope:** Windows, personal accounts, provider-reported data only.

### Stage 1: Manually Use A Chosen Claude Or Codex Reset

1. Show each currently available reset grant with its provider name, window and expiry. Keep the
   existing count as a summary; enable use only when the API supplies a stable record that can be
   revalidated.
2. Require an explicit confirmation naming the account and exact reset selected.
3. Immediately before the claim, fetch the provider's reset list again and verify that the same
   record is still available and eligible.
4. Use the provider-supported authorization and endpoint. If Claude requires an additional scope,
   explain when the user must sign in again; never replace a saved credential until sign-in succeeds.
5. Never claim automatically or automatically repeat an ambiguous POST. Refresh the grant state and
   require a new explicit action if the outcome remains uncertain.
6. Update `README.md` and `docs/windows.md`; add focused checks for confirmation, stale grants,
   provider errors, success and unknown claim outcomes.

**Acceptance:** No reset can be claimed without confirmation and a fresh match for the exact chosen
record. A successful claim refreshes the account, and an uncertain result is never automatically sent
again.

**Upstream basis:** [v0.8.0-beta.7](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.7),
[manual reset claims, PR #1642](https://github.com/robinebers/openusage/pull/1642).

### Stage 2: Add Provider-Reported Usage History

1. Add a separate History view for providers with supported history APIs: ChatGPT/Codex, Cursor,
   Ollama and OpenRouter. Keep daily/token/activity data out of the limits dashboard and tray metrics.
2. Show only values and events returned by each provider. Do not derive prices, infer missing usage,
   or inspect local CLI logs.
3. Make the selected account and reported time range clear; handle providers with no history or
   partial data without presenting missing values as zero.
4. Fetch history on demand and respect each provider's API limits. Update `README.md` and
   `docs/windows.md` when the view ships.

**Acceptance:** History is separated from the quota dashboard, attributed to the correct account and
time range, and contains no estimated usage or cost.

**Upstream basis:** [v0.8.0](https://github.com/robinebers/openusage/releases/tag/v0.8.0),
[Cursor history, PR #1501](https://github.com/robinebers/openusage/pull/1501),
[Ollama history, PR #1520](https://github.com/robinebers/openusage/pull/1520),
[OpenRouter activity, PR #1523](https://github.com/robinebers/openusage/pull/1523),
[ChatGPT history, PR #1524](https://github.com/robinebers/openusage/pull/1524).

### Smaller Upstream Fixes To Validate

These have clear counterparts in the current Windows implementation and are smaller than the two
features above. Confirm provider units and behavior with fixtures before changing the mappers or
refresh cache.

- **Grok cap:** `onDemandCap.val` is currently displayed as a raw `Cap`; upstream treats the value as
  cents. Convert it to dollars only after confirming the current response contract.
- **Cursor budget pairing:** the mapper can select an individual cap and then use pooled spend, or
  vice versa. Keep cap, remaining and spend from the same budget source.
- **Claude secondary reads:** a failed optional profile/grant request is cached as `null` for an hour.
  Retry transient failures without bypassing provider rate-limit delays.

**Upstream basis:** [Grok cap units](https://github.com/robinebers/openusage/commit/b7d81db2b99d13c145ad0f4c5e4583373d53510b),
[Cursor budget pairing](https://github.com/robinebers/openusage/commit/b3f6e7920d54318b4fb02ccd08d878abaea139e5),
[Claude grant retry](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.8).
