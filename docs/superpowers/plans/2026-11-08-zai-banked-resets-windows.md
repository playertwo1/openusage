# Z.ai Personal Banked Resets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a personal Z.ai account's banked Session and Weekly resets and let the user manually spend only the exact earliest reset after confirming.

**Architecture:** Read reset records in the existing secondary-refresh path and keep them separate from the main quota response. Parse them into typed records; a dedicated service owns fresh-list validation and a single manual claim. A failed secondary read never invalidates the regular quota metrics.

**Tech Stack:** C# 14, .NET 10, existing `Api`, `System.Text.Json.Nodes`, WPF/Windows Forms, Windows console check harness.

**Spec:** [Roadmap, Stage 3](../../roadmap.md#stage-3-read-and-manually-use-zai-personal-banked-resets); upstream [Z.ai reset work](https://github.com/robinebers/openusage/pull/1576).

## Global Constraints

- Use only `targetType=PERSONAL` and the API key already saved for the selected Z.ai account. Do not add Team behavior or discover credentials elsewhere.
- The regular quota endpoint remains authoritative for Session, Weekly and Web Searches. Reset-list errors must not hide those metrics or erase the last valid list.
- Never spend a reset automatically. Every POST requires an explicit confirmation naming the reset window.
- Before implementing the Use action, verify the current Z.ai Personal endpoint and response contract from a primary source. The upstream PR says its list and claim were not tested live against Z.ai.
- Before adding the `Limit Resets` metric, ask the owner to confirm its tray pin and position in Z.ai's metric order; keep it shown by default.
- Never retry an ambiguous claim POST automatically; never log credentials or response bodies.
- Start by checking `git status --short`; preserve existing edits and isolate work if target files are already modified.

## Review Focus

- Missing record ID, grant type, window or invalid expiry: the record cannot be displayed as claimable and no POST occurs.
- Two records with the same expiry: display deterministically and validate the exact record ID, not expiry alone.
- Confirmed record is no longer earliest or no longer available: refuse the POST and refresh details.
- List read failure: retain regular quota metrics and last valid reset data; show an account-specific error without response content.
- Claim timeout/unknown response, account removal during confirmation, or success without `success: true`: never automatically issue another POST.

---

### Task 1: Parse and validate personal banked-reset records

**Files:**
- Modify: `Windows/OpenUsage.Windows/Models.cs`
- Modify: `Windows/OpenUsage.Windows/UsageMappers.cs`
- Create: `Windows/OpenUsage.Windows/ZaiResetService.cs`
- Create: `Windows/OpenUsage.Windows.Tests/ZaiResetChecks.cs`
- Modify: `Windows/OpenUsage.Windows.Tests/Program.cs`

**Interfaces:**
- Produces: `ZaiResetWindow { Session, Weekly }` and `ZaiReset(Window, RecordId, ExpiresAt, GrantType)`.
- Produces: `UsageSnapshot.ZaiResets`, optional and empty for non-Z.ai providers.
- Produces: `ZaiResetService.ListAsync(string apiKey, CancellationToken ct) -> Task<IReadOnlyList<ZaiReset>>`.

- [x] **Step 1: Add failing parser/service checks** for available vs expired records, Session/Weekly separation, earliest-first stable ordering, UTC+8 expiry parsing, missing fields, and two records sharing an expiry.
- [ ] **Step 2: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new Z.ai reset assertions.
- [x] **Step 3: Implement the typed record parser** for the verified response contract. Accept only available, unexpired records. Parse `yyyy-MM-dd HH:mm:ss` as UTC+8 and store UTC. Reject the entire secondary list if a potentially claimable record lacks a non-empty `recordId`, `grantType`, known reset type or valid expiry.
- [x] **Step 4: Implement `ListAsync`** with the existing `Api` and the personal endpoint `https://api.z.ai/api/biz/customer-package-reset/list?targetType=PERSONAL`. Sort by expiry within each window and preserve response order for equal expiries.
- [x] **Step 5: Run `./script/windows.ps1 test`.**
  **Expected:** PASS for parsing, timezone, ordering and malformed-response checks.
- [ ] **Step 6: Commit the task** after checking the staged diff contains only intended files.

### Task 2: Integrate the hourly secondary read without breaking quota refresh

> **Status (2026-11-08): superseded.** The owner removed the Z.ai provider from the Windows edition and does not use its API, so this stage's code was removed from the worktree (`windows-personal-usage-phase`, uncommitted). The reset-list ideas are kept for other providers, which already show their own reset counts.

**Files:**
- Modify: `Windows/OpenUsage.Windows/UsageService.cs`
- Modify: `Windows/OpenUsage.Windows/UsageMappers.cs`
- Modify: `Windows/OpenUsage.Windows/MainForm.cs`
- Test: `Windows/OpenUsage.Windows.Tests/ZaiResetChecks.cs`
- Test: `Windows/OpenUsage.Windows.Tests/UsageChecks.cs`

**Interfaces:**
- Consumes: `ZaiResetService.ListAsync` and `UsageSnapshot.ZaiResets` from Task 1.
- Produces: `UsageMappers.Zai(JsonNode body, JsonNode? subscription = null, IReadOnlyList<ZaiReset>? resets = null)` and a Z.ai branch in `UsageService.RefreshSecondaryAsync`.

- [x] **Step 1: Add failing refresh checks** for hourly caching per API key, account isolation, reset-list failure, and preservation of Session/Weekly/Web Searches.
- [ ] **Step 2: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new Z.ai secondary-refresh checks.
- [x] **Step 3: Cache the primary Z.ai usage response by account token** and fetch resets in the secondary-refresh slot, no more than once per hour. A manual refresh of reset details may bypass the hourly cache.
- [ ] **Step 4: Merge reset data in `UsageMappers.Zai`.** Append `Limit Resets` after Web Searches only when the count is above zero. Do not choose its pinned default or metric order until the owner decision in the roadmap is recorded.
- [x] **Step 5: Extend `MainForm.RefreshSecondaryAsync` to Z.ai.** On a secondary error, keep the existing snapshot and last valid reset list; surface a friendly account-specific message. Do not fail the main provider refresh.
- [x] **Step 6: Run `./script/windows.ps1 test`.**
  **Expected:** PASS for fetch routing, hourly cache, account isolation and secondary-failure behavior.
- [ ] **Step 7: Commit the task** after checking the staged diff contains only intended files.

### Task 3: Add a confirm-before-use reset details flow

> **Status (2026-11-08): stopped at Step 1.** Z.ai's public docs (docs.z.ai) do not document the reset list or claim endpoints. The only sources are third-party projects and the upstream OpenUsage PR, none of which were verified live. Per the gate, the Use action is not implemented and this task stays pending.

**Files:**
- Modify: `Windows/OpenUsage.Windows/ZaiResetService.cs`
- Modify: `Windows/OpenUsage.Windows/DashboardView.Manage.cs`
- Modify: `Windows/OpenUsage.Windows/DashboardView.cs`
- Modify: `Windows/OpenUsage.Windows/MainForm.cs`
- Create: `Windows/OpenUsage.Windows/ZaiResetDialog.cs`
- Test: `Windows/OpenUsage.Windows.Tests/ZaiResetChecks.cs`
- Test: `Windows/OpenUsage.Windows.Tests/UiChecks.cs`

**Interfaces:**
- Consumes: typed records and the hourly reset list from Tasks 1–2.
- Produces: `ZaiResetService.UseAsync(string apiKey, ZaiReset selected, CancellationToken ct) -> Task<ZaiResetClaimOutcome>` with outcomes `Succeeded`, `NoLongerEarliest`, `Rejected` and `Unknown`.

- [ ] **Step 1: Verify the claim contract from a primary source.** Confirm the `api.z.ai` host, API-key auth, request fields and success response without sending a live POST. If this evidence is unavailable, stop this task at the read-only list, omit the Use button, and keep Stage 3 pending; do not guess the contract.
- [ ] **Step 2: Add failing claim checks** for a fresh-list recheck, no POST for a stale/non-earliest record, exact record body, `success: true`, non-success response, timeout, and no automatic retry.
- [ ] **Step 3: Run `./script/windows.ps1 test`.**
  **Expected:** FAIL at the new claim assertions.
- [ ] **Step 4: Implement `UseAsync`.** Bypass list cache; confirm the selected exact `recordId` is still the earliest available record in its Session or Weekly window. POST once to `https://api.z.ai/api/biz/customer-package-reset/use` with `targetType=PERSONAL`, `resetType=FIVE_HOUR` or `WEEK`, the selected record's exact `recordId` and `grantType`, and a new UUID `requestId`. Treat only HTTP success plus JSON `success: true` as Succeeded. A timeout or lost response is Unknown; do not retry it.
- [ ] **Step 5: Build the Accounts details dialog.** Show Session and Weekly records separately, earliest first, including expiry. Expose Use only for the first available record of each window. The Use button opens a confirmation that names the window and says the action cannot be undone; Cancel sends no request.
- [ ] **Step 6: Revalidate the account before POST.** If the account was removed while the dialog was open, close without sending. On `NoLongerEarliest`, refresh details and ask the user to confirm again. On `Unknown`, refresh the list for information but never repeat the POST automatically.
- [ ] **Step 7: After success, refresh the reset list and quota snapshot.** Keep Session and Weekly records independent; a claim in one window must not block use in the other.
- [ ] **Step 8: Run `./script/windows.ps1 test`.**
  **Expected:** PASS for the complete mocked claim and UI confirmation flow.
- [ ] **Step 9: Commit the task** after checking the staged diff contains only intended files.

### Task 4: Document the Windows behavior

**Files:**
- Modify: `README.md`
- Modify: `docs/windows.md`

- [ ] **Step 1: Document** the personal reset list, hourly refresh, exact-record confirmation, and the fact that reset use is manual and irreversible.
- [x] **Step 2: Document the gate accurately.** If the claim contract was not verified, state that Use remains unavailable. If it was verified and enabled, document the confirmation and no-automatic-retry behavior.
- [x] **Step 3: Run `./script/windows.ps1 test`.**
  **Expected:** PASS.
- [ ] **Step 4: Commit the task** after checking the staged diff contains only intended files.
