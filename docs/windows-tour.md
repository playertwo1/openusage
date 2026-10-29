# Windows Product Tour


The Windows edition lives in [`Windows/`](../Windows/) and is independent of the Swift app and the macOS release pipeline. It is a tray app written in C# on .NET 10. The screens are WPF, hosted by the Windows Forms window and tray lifecycle. It supports the same ten providers (Claude, ChatGPT/Codex, Cursor, Antigravity, Copilot, Grok, Ollama, OpenCode, OpenRouter and Z.ai) and follows the same rule as the Mac app: you sign in to each account inside the app, and it never reads credentials or files that belong to other tools.

**Download:** the portable `OpenUsage-windows-x64.exe` (69 MB, no installer, runs without installing .NET) is on the [Windows 0.1.0 release](https://github.com/playertwo1/openusage/releases/tag/windows-v0.1.0). To build it yourself, run `./script/windows.ps1 publish` with the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Version history is in [`Windows/CHANGELOG.md`](../Windows/CHANGELOG.md) and details are in [docs/windows.md](windows.md). All screenshots below use made-up demo data, not real accounts.

### Overview

<p align="center">
  <img src="images/windows/overview.png" alt="Windows Overview in the light theme, with an expanded row showing the weekly pace chart" width="320">
  <img src="images/windows/overview-dark.png" alt="Windows Overview in the dark theme showing remaining values" width="320">
</p>

Overview is one grouped list with a row per account, in the order you saved. A column header names the two windows: **5h** (the session limit, purple) and **Week** (the weekly limit, cyan). Those two colors mean the same thing everywhere in the app.

Each row shows:

- **A double ring.** The outer ring is the 5h Session window and the inner ring is Weekly. Providers with only one window get a single ring.
- **The account name** with the provider icon, a star when you have starred a metric, and a short verdict under it: "Lasts week", "Runs out Sat" or "5h window", plus how far ahead or behind an even pace you are (for example "+22%" or "on pace").
- **Both windows** with the percentage, the time until reset and a thin bar. The Weekly bar has a small tick where an even pace would be right now.
- **An alert on the number only.** The number turns red from 85% used, and amber when the weekly projection passes 100%. Bars and rings never change color, so the purple and cyan stay recognizable.

Click a row (or press Enter or Space with it focused) to open its **weekly pace chart**. It plots your cumulative weekly use against the ideal line of 14.3% a day, adds a dashed projection to day 7, and marks where the limit would run out. Next to it are the verdict, "Up to X%/day left" and "Projected N%", and a "Daily" line with the use of each day since the reset. Only one row is open at a time. Right-click a row for rename, remove and move actions.

The pace is computed from the provider's own percentage and reset time over a 168-hour window: `expected = elapsed / 168h`, `projection = used / elapsed * 168h`, and the day the limit runs out is when use would reach 100% at the current rate. The daily numbers come from the weekly percentages OpenUsage has actually seen, so a day when the app was closed shows a dash. **There is no spend, token or price estimate anywhere**, because the providers do not report those and OpenUsage does not infer them.

The switches above the list choose **Used** or **Remaining** (the values, rings, bars and pace tick all flip) and **All** or **Favorites**.

### Accounts

<p align="center">
  <img src="images/windows/accounts.png" alt="Windows Accounts screen with one list per account and a row per metric" width="320">
</p>

Accounts are grouped by provider, with the provider menu in the group header. Each account is its own list: a header with the name, a plan badge (Pro, Max, Plus and so on) and an account menu, then **one row per metric** with the favorite star, the name, a bar, the value, the reset time and a **Show** switch. "Limit Resets" is a plain row without a bar. Each account ends with its "Updated" time. Here you rename and remove accounts, reorder accounts and providers, star up to two metrics per account for the tray, and hide metrics. Every metric starts shown, and the starred defaults match the Mac app.

### Settings

<p align="center">
  <img src="images/windows/settings.png" alt="Windows Settings screen with Appearance, Usage Display and General groups" width="320">
</p>

Settings is a short list in three groups.

- **Appearance:** theme (System, Light or Dark) and text size (100%, 110% or 125%).
- **Usage Display:** show usage as used or remaining, reset times as a countdown or a date and time, favorites only, and hide account names. Hiding names applies to Overview, the tray, the tray tooltip and notifications. Accounts still shows them so you can manage them.
- **General:** launch at sign-in, low usage alerts (a notification when a limit passes 90%), the global shortcut Ctrl+Alt+U, and the mini widget options below.

### Mini widget

<p align="center">
  <img src="images/windows/mini-widget.png" alt="Horizontal mini widget" height="64">
  &nbsp;
  <img src="images/windows/mini-widget-vertical.png" alt="Vertical mini widget" height="120">
  &nbsp;
  <img src="images/windows/mini-widget-flyout.png" alt="Mini widget flyout with one row per account" width="300">
</p>

An optional small panel that floats on your desktop. Turn it on from **Settings > General**, the tray menu ("Show Mini Widget").

- **The pill:** a short label per account (C1, C2, Cx, AG, Gk) with two thin bars, 5h on top and Week below, and a dot when use passes 85% or the weekly projection passes 100%. A vertical layout is available for screen edges.
- **Drag it anywhere** by its surface. It snaps to a screen edge when released within 12 px, remembers its place **per monitor**, and moves back into view if your resolution or monitors change.
- **Hover (or click)** to open a flyout with every account's windows, reset times and verdict. It opens on whichever side has room and closes when the mouse leaves.
- **Right-click** for Always on top, Lock position, Vertical layout, Open OpenUsage, Refresh now and Hide widget.
- It has no border, stays out of the taskbar and Alt+Tab, and never takes keyboard focus.

### Adding an account

<p align="center">
  <img src="images/windows/add-account.png" alt="Add account screen with the provider selector" width="320">
  <img src="images/windows/empty.png" alt="First-run empty state" width="320">
</p>

**Add Account** opens a provider picker. Claude, ChatGPT, Cursor, Antigravity, Copilot and Grok sign in through your browser or a device code. Ollama, OpenCode, OpenRouter and Z.ai use API keys, and OpenRouter's balance needs a Management key. You can add several accounts per provider. Sign-in tokens are renewed automatically when due, and a failed early renewal keeps a still-valid token and shows an account-specific warning.

### How your data is kept

- Accounts and tokens are saved in `%LOCALAPPDATA%\OpenUsage.Windows\accounts.dat`, encrypted with Windows DPAPI for your user account. Tokens and provider responses are never logged.
- The file also stores your display choices, favorites, hidden metrics and the weekly percentages used for the daily history. Older files without these load with defaults, and your accounts and ordering are preserved.
- If you run the app through a packaged (Microsoft Store) host such as the Codex app, Windows may redirect that folder to a private location. Launch `OpenUsage.exe` directly so it always uses the same file.

### What changed from the first Windows build

The first Windows build worked but looked plain. This edition keeps its sign-ins, ten provider clients and encrypted storage, and changes everything you see:

- A WPF interface modeled on the Mac app, replacing the plain Windows Forms controls, with original provider logos and a compact window you move by its header and resize by its edges.
- Overview, Accounts and Settings rebuilt as described above, with light, dark and system themes and larger text sizes.
- The double rings, pace marker and weekly pace chart, which need no extra data from providers.
- A purple accent and fixed purple and cyan colors for the two windows, replacing the earlier blue.
- The optional floating mini widget, its flyout, menu and per-monitor position.
- A name privacy option, favorites, per-metric visibility and reordering that now also cover the tray and notifications.
- The portable executable shrank from 173 MB to 69 MB.
- Regression checks for storage, compatibility with older account files, token renewal, usage mapping and the rendered screens, run by `./script/windows.ps1 test`.

Known limits: the window is opaque (no Acrylic or Mica), the shortcuts are fixed, and there is no installer or automatic update.
