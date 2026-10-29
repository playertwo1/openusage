# Windows

The Windows development edition is a separate C#/.NET application in `Windows/`, with WPF views
hosted by the Windows Forms tray/window lifecycle.
The Swift app and macOS release pipeline remain independent. This is a local development build,
published separately as the `windows-v*` releases, such as 0.1.0.

## Build And Run

Use Windows 10 or Windows 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
From PowerShell in the repository:

```powershell
./script/windows.ps1 run
./script/windows.ps1 test
./script/windows.ps1 publish
```

`publish` creates `dist/windows/win-x64/OpenUsage.exe`, a portable executable that includes its .NET
runtime. Use `-Runtime win-arm64` for Windows on Arm. Windows CI builds, tests and uploads the x64
executable separately from macOS releases.

## Accounts And Usage

Use **Add Account** to sign in. Claude, ChatGPT, Cursor, Antigravity, Copilot and Grok use browser or
device-code sign-in. Ollama, OpenCode, OpenRouter and Z.ai use API keys. OpenRouter balance requires
a Management key. Multiple accounts per provider are supported.

OAuth tokens are renewed automatically when due. Cursor renews seven days early, matching Swift,
because its access JWT also becomes its next refresh token. A failed early renewal keeps a still-valid
token usable and displays an account-specific warning; an expired or rejected sign-in requires login.

Provider response mapping and metric defaults follow the current Swift edition. All metrics start
shown. Providers keep insertion order; a new account goes after its provider's existing accounts.
**Accounts** supports renaming, removal, provider/account reordering and metric visibility. Right-click
a metric to reorder it. Stars choose tray-menu favorites, initially matching the Swift defaults.
The Windows tray has an icon and a short hover summary rather than the macOS text strip.

## Visualization

**Overview** has quick switches for **Used / Remaining** and **All / Favorites**. Remaining values are calculated only for provider-reported percentages or bounded limits; balances and other unbounded values retain their reported meaning.

**Accounts** groups accounts by provider. Each account is one list with a row per metric: favorite star, name, bar, value, reset and a Show switch. Right-click a row for move and hide actions.

**Settings** saves theme (system, light or dark), text size (100%, 110%,
125%), usage and reset format, favorites filtering and account-name privacy. Privacy hides labels in Overview,
tray favorites, the tray summary and notification titles. Accounts still shows them for management.
The focusable provider action button supports keyboard reordering. The window can be moved by its header and resized at its
edges. The close control hides it in the tray. Existing account files load with display defaults;
credentials and account/metric ordering are preserved.

Usage refreshes every five minutes; **Refresh** starts a manual pass. Overlapping passes are skipped.
Claude's hourly profile and reset-grant reads run 150 seconds after a usage pass to avoid requesting
them in the same burst. HTTP 429 waits survive app restarts: one hour for Claude and five minutes
for other providers. Manual refresh respects those waits. Errors appear under the affected account.
Last successful values stay visible, marked out of date after five minutes. Removing an account
cancels its active refresh and discards late responses.

## Windows Integration And Storage

Closing the window leaves the tray icon running. Click it or press **Ctrl+Alt+U** to toggle the panel.
Right-click the icon and choose **Exit** to quit. If the shortcut is in use by another app, use the
tray icon. **Settings** can enable startup at Windows sign-in and alerts below 10% remaining.

Metadata, preferences and credentials are encrypted together with Windows DPAPI for the current
Windows user, under `%LOCALAPPDATA%\OpenUsage.Windows\accounts.dat`. Removing an account deletes its
credential and customization. The app reads no credentials from other tools. Writes replace the
encrypted file atomically; an unreadable file is reported instead of overwritten. **Open Data Folder**
also exposes `errors.log`: error types and HTTP status codes, never credentials, bodies or exception
messages. No telemetry is sent by this Windows implementation.

## Verification And Current Limits

Regression checks cover protected storage, ordering, OAuth grants/callbacks and fixture-based mapping
for all ten providers without real accounts. Real sign-in and quotas require manual verification
with accounts the user adds. OAuth providers may restrict client or operating-system behavior.

This first Windows implementation covers sign-in, consumption, tray favorites and basic settings.
It does not yet include the macOS CLI/cache export, screenshot sharing, spending a limit reset,
automatic application updates or an installer. Provider-reported reset counts can
be shown; the Windows app does not spend them. The macOS release workflow does not publish Windows.

Overview is one grouped list with a row per account, in the saved order. Each row has a double ring (outer is the 5h Session, inner is Weekly), the two windows with their reset times, and a pace marker on the Weekly bar. Colours identify the window and never change with use; a number turns red from 85% used and amber when the weekly projection passes 100%. Click a row to open the weekly pace chart: cumulative use against an even 14.3% a day, the projection, and the day the limit runs out. Daily use comes from the provider's weekly percentages that OpenUsage saw, so days it was not running show a dash. There is no spend or token total: the providers do not report one. Favorites, hiding and reordering metrics live on the Accounts screen.


**Mini Widget** is an optional small floating panel (Settings, the tray menu or Ctrl+Alt+W). It shows a short label and two thin bars per account: 5h on top, Week below, with a dot when use passes 85% or the weekly projection passes 100%. Drag it anywhere; hovering or clicking opens a flyout with every account's windows, reset times and pace. Right-click for Always on top, Lock position, Vertical layout, Open OpenUsage, Refresh now and Hide widget. The window is borderless, stays out of the taskbar, does not take focus, snaps to screen edges within 12px, and remembers its place per monitor, moving back into view when the screen setup changes. Like the main window it is opaque, with no translucent material.
