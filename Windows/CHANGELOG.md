# OpenUsage for Windows

## 0.1.0

First release of the Windows edition: a C#/.NET 10 tray app that shows the usage limits of the AI coding accounts you add inside it (Claude, Codex, Cursor, Copilot, Antigravity, Grok, Ollama, OpenCode, OpenRouter, Z.ai).

- **Sign-in and storage:** you sign in to each provider from the app. Accounts and tokens are kept in a file protected by your Windows account. Nothing is read from other tools.
- **Overview:** one list with a row per account. Each row has a double ring (5h Session outside, Weekly inside), both windows with their reset times, and a pace marker on the Weekly bar. A number turns red from 85% used and amber when the weekly projection passes 100%. Click a row for the weekly pace chart: use against an even 14.3% a day, the projection and the day the limit runs out.
- **Used / Remaining** and **All / Favorites** switches, in the saved account order.
- **Accounts:** accounts grouped by provider, one row per metric with favorite star, bar, value, reset and a Show switch. Rename, remove and reorder accounts, providers and metrics.
- **Settings:** theme (system, light, dark), text size (100%, 110%, 125%), used or remaining, reset format, favorites only, hide account names, launch at sign-in, low usage alerts.
- **Mini widget:** an optional floating panel you drag anywhere, with a hover flyout, a right-click menu, a vertical layout and a position remembered per monitor. Toggle it from the tray, Settings.
- **Tray:** favorites in the tooltip and menu, Ctrl+Alt+U to open the panel, notifications when a limit passes 90%.
- **Weekly history:** daily use comes from the provider's weekly percentages that the app saw, so days it was closed show a dash. There is no spend or token total, because the providers do not report one.

Known limits: the window is opaque (no Acrylic or Mica), the shortcuts are fixed, and there is no installer or automatic update. Run the portable `OpenUsage.exe` from `./script/windows.ps1 publish`.
