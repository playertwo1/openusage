# Windows

The Windows development edition is a separate C#/.NET application in `Windows/`, with WPF views
hosted by the Windows Forms tray/window lifecycle.
The Swift app and macOS release pipeline remain independent. This is a local development build,
published separately as the `windows-v*` releases, such as 0.1.1.

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

The panel adjusts its height when you change pages, filters, accounts or expand a weekly chart.
Its maximum height is the monitor's work area; excess content remains reachable by scrolling.
Settings → Appearance → Soft Transparency switches between slight whole-window transparency
and an opaque panel. High contrast always uses an opaque panel. This effect does not blur the desktop.
The mini widget uses compact provider labels (numbered for multiple accounts of the same provider),
two percentage rows, and the same soft-transparency preference. Hover for account details.
Dragging the widget within 32 logical pixels of a work-area edge attracts it to that edge with a
6-pixel margin. Corners attract on both axes, and the saved position is restored per monitor.
The widget starts collapsed to a 32-logical-pixel ball. Click it to show the mini panel; click it again, use the minus
button or the right-click menu to collapse it again. Resizing is immediate and needs no refresh. The collapsed state is saved. Both modes can
be dragged and snapped to edges. Alert dots are not shown on the mini panel.
Hovering over the ball also opens the usage panel without expanding the mini. Click an account
in that panel to show its weekly pace chart in place; click again to close it. Only one chart is
expanded at a time. Accounts without a weekly reset time show an explanation instead of a chart.

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
The header's **Close Window** (X) button also hides only the panel. The executable, taskbar and
desktop shortcut use the embedded OpenUsage icon. Use **Exit OpenUsage** to end the process.
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

Overview keeps accounts in saved order with a separate heading and two metric lines per account.
Click the account heading to expand its weekly pace chart. The chart compares observed weekly
usage with an even pace and shows the projection. No spend or token totals are estimated.
Favorites, metric visibility and reordering live on the Accounts screen.

**Mini Widget** is an optional floating panel, enabled in Settings or the tray menu. It can be
collapsed to a ball. Hover over the ball for account usage, then click an account to expand its
chart in that panel. Clicking the ball opens the compact mini with two bars and percentages per
account; clicking the mini again or its minus button collapses it. Drag either view to snap it to edges and corners.
The widget stays out of the taskbar, does not take focus and remembers its position per monitor.
Soft Transparency is optional; it does not provide Acrylic, Mica or Liquid Glass blur.
