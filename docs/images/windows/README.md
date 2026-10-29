# Windows Screenshots

These images are rendered from the Windows application's current WPF controls, using synthetic
accounts and provider responses from `Windows/OpenUsage.Windows.Tests/UiChecks.cs`. They are not
AI mockups and contain no real account names, credentials or usage data.

Generate a fresh set with the .NET 10 SDK:

```powershell
dotnet run --project Windows/OpenUsage.Windows.Tests -c Release -- --render <output-directory>/overview.png
```

The base image is Overview in light mode. Suffixes identify dark mode, Accounts, Settings,
Add Account, empty state, compact widget, vertical widget, ball, hover panel and expanded hover
chart. Reset times and dates are relative to the render time. Refresh these images when the
corresponding controls change. Screenshots show the source build, which may be newer than the
latest published executable.
