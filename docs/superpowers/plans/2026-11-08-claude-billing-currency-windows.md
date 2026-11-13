# Claude Personal Billing Currency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render Claude Extra Usage in the billing currency and minor-unit precision reported by that personal account.

**Architecture:** Preserve the currency code and decimal-place metadata on the existing `Metric` value. Keep the current USD behavior as the fallback and format currency consistently through the existing metric presentation path.

**Tech Stack:** C# 14, .NET 10, `System.Text.Json.Nodes`, the existing Windows console check harness.

**Spec:** [Roadmap, Stage 1](../../roadmap.md#stage-1-show-claude-extra-usage-in-its-billing-currency); upstream [currency fix](https://github.com/robinebers/openusage/commit/8a88321).

## Global Constraints

- Change only `Windows/` behavior and its Windows documentation; the Swift/macOS app remains separate.
- Preserve the existing `Metric` constructor call sites by appending optional fields.
- Use Claude's provider-reported currency and precision; do not infer exchange rates or convert between currencies.
- An absent or invalid currency code defaults to USD; an absent or invalid decimal-place count defaults to 2, independently.
- Start by checking `git status --short`; preserve existing edits and isolate work if target files are already modified.

## Review Focus

- `extra_usage` disabled or missing: Claude's other metrics still map, and existing USD fixtures stay unchanged.
- EUR or JPY metadata: amount, cap and Remaining value use the same code and precision.
- Missing, malformed or out-of-range `currency` / `decimal_places`: values remain finite and use the documented USD fallback.
- `Text` overrides and non-currency metrics: existing output is unchanged.
- Currency output in Overview, Accounts, mini widget and tray: all use the same formatting path.

---

### Task 1: Carry validated billing metadata through Claude mapping

**Files:**
- Modify: `Windows/OpenUsage.Windows/Models.cs`
- Modify: `Windows/OpenUsage.Windows/UsageMappers.cs`
- Test: `Windows/OpenUsage.Windows.Tests/UsageChecks.cs`

**Interfaces:**
- Consumes: `UsageMappers.Claude(JsonNode body, JsonNode? profile = null, JsonNode? grants = null, DateTimeOffset? now = null)`.
- Produces: `Metric.CurrencyCode` and `Metric.CurrencyDecimalPlaces`, appended as optional constructor fields after `Text`.

- [x] **Step 1: Add failing mapper checks** for the current USD fixture, EUR with two decimal places, JPY with zero decimal places, and invalid metadata. Assert normalized values, limit and currency fields.
- [x] **Step 2: Run the Windows check harness** with `./script/windows.ps1 test`.
  **Expected:** FAIL at the new currency metadata assertions.
- [x] **Step 3: Append optional currency fields to `Metric`** and update only Claude's `extra_usage` mapping. Normalize a valid three-letter ASCII ISO code to uppercase; accept an integer `decimal_places` from 0 through 6; default an invalid/missing code to USD and an invalid/missing precision to 2 independently. Convert `used_credits` and `monthly_limit` by `10^decimal_places`.
- [x] **Step 4: Run `./script/windows.ps1 test`.**
  **Expected:** PASS, including existing USD and provider mapper checks.
- [ ] **Step 5: Commit the task** after checking the staged diff contains only intended files.

### Task 2: Format all bounded and unbounded Claude currency values consistently

**Files:**
- Modify: `Windows/OpenUsage.Windows/Models.cs`
- Modify: `Windows/OpenUsage.Windows/DisplayPreferences.cs`
- Test: `Windows/OpenUsage.Windows.Tests/UiChecks.cs`

**Interfaces:**
- Consumes: `Metric.CurrencyCode` and `Metric.CurrencyDecimalPlaces` from Task 1.
- Produces: `Metric.FormatAmount(double amount) -> string`, used by `Metric.Display` and `MetricPresentation.Value` for currency metrics.

- [x] **Step 1: Add failing presentation checks** for USD, `EUR 12.34`, zero-decimal JPY, and a bounded Remaining value. Assert that `Text` overrides and non-currency units retain their existing output.
- [x] **Step 2: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new presentation assertions.
- [x] **Step 3: Implement `Metric.FormatAmount(double amount)`** so USD retains `$` and all currencies use their declared precision (defaulting to two decimals). Other codes use an unambiguous ISO-code prefix. Route `Metric.Display` and the bounded Remaining path through it; leave percentage display unchanged.
- [x] **Step 4: Run `./script/windows.ps1 test`.**
  **Expected:** PASS for mapper, presentation, storage, and rendered UI checks.
- [ ] **Step 5: Commit the task** after checking the staged diff contains only intended files.

### Task 3: Document the Windows behavior

**Files:**
- Modify: `README.md`
- Modify: `docs/windows.md`

- [x] **Step 1: Update both documents** to state that Claude Extra Usage uses the account's currency and minor-unit precision, with USD/2 as fallback.
- [x] **Step 2: Review the wording** against the USD, EUR and JPY assertions from Tasks 1–2.
- [x] **Step 3: Run `./script/windows.ps1 test`.**
  **Expected:** PASS; documentation changes do not alter the Windows build.
- [ ] **Step 4: Commit the task** after checking the staged diff contains only intended files.
