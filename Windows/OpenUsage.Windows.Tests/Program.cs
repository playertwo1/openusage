using OpenUsage.Windows;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

var directory = Path.Combine(Path.GetTempPath(), "OpenUsage-Tests-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    var store = new AccountStore(directory);
    var a = new Account(Guid.NewGuid(), ProviderKind.Codex, "First", new Credential("test-secret-one"));
    var b = new Account(Guid.NewGuid(), ProviderKind.Claude, "Second", new Credential("test-secret-two"));
    var c = new Account(Guid.NewGuid(), ProviderKind.Codex, "Third", new Credential("test-secret-three"));
    store.Add(a); store.Add(b); store.Add(c);
    Check(store.State.Accounts.Select(x => x.Id).SequenceEqual(new[] { a.Id, c.Id, b.Id }), "Accounts Must Stay In Provider Insertion Order");
    Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "accounts.dat"))).Contains("test-secret"), "Credentials Must Be Encrypted At Rest");
    Check(new AccountStore(directory).State.Accounts[0].Credential.AccessToken == "test-secret-one", "DPAPI Round Trip");
    store.MoveAccount(c.Id, -1);
    Check(store.State.Accounts[0].Id == c.Id, "Account Reorder");
    store.MoveProvider(ProviderKind.Claude, -1);
    Check(store.State.Accounts[0].Id == b.Id, "Provider Reorder");
    Check(store.IsPinned(a, new Metric("Session", 10)), "Existing Codex Pin Default");
    Check(!store.IsPinned(a, new Metric("Spark", 10)), "Other Metrics Must Not Be Pinned By Default");
    store.Edit(s => s.Hidden.Add(AccountStore.MetricKey(a.Id, "Weekly")));
    store.Remove(a.Id);
    Check(!store.State.Hidden.Any(), "Removing An Account Must Clear Its Layout Settings");
    var oldCount = store.State.Accounts.Count;
    try { store.Add(c); throw new Exception("Duplicate Account Accepted"); } catch (InvalidDataException) { }
    Check(store.State.Accounts.Count == oldCount, "Failed Saves Must Not Mutate State");
    var until = DateTimeOffset.UtcNow.AddHours(1);
    store.Edit(s => s.RetryAfter[c.Id] = until);
    Check(new AccountStore(directory).State.RetryAfter[c.Id] == until, "Rate Limit Wait Must Survive Relaunch");
    Check(!AccountStore.ValidSecret("secret\r\nInjected: value"), "Reject Header Injection");
    var rotated = new Credential("rotated-access", "rotated-refresh");
    store.Replace(c with { Credential = rotated });
    store.Rename(c.Id, "New Name");
    Check(store.State.Accounts.First(x => x.Id == c.Id).Credential.RefreshToken == "rotated-refresh", "Rename Must Preserve Concurrently Rotated Credentials");
    Check(JsonNode.Parse("true").Number() is null, "Booleans Are Not Numbers");
    Check(JsonValue.Create(42).Number() == 42, "CLR Integer JSON Values Are Numbers");
    var now = DateTimeOffset.UtcNow;
    var expiring = new Credential("access", "refresh", now.AddDays(6));
    Check(ProviderLogin.NeedsRefresh(ProviderKind.Cursor, expiring, now), "Cursor Must Renew Before A Week-Long Sleep");
    Check(!ProviderLogin.NeedsRefresh(ProviderKind.Codex, expiring, now), "Other Providers Must Keep Their Existing Renewal Margin");
    Check(!ProviderLogin.NeedsRefresh(ProviderKind.Cursor, expiring with { ExpiresAt = now.AddDays(8) }, now), "Cursor Must Not Renew Too Early");
    Check(ProviderLogin.NeedsRefresh(ProviderKind.Codex, expiring with { ExpiresAt = now.AddMinutes(4) }, now), "Normal Five-Minute Renewal Margin");
    Check(JsonNode.Parse("\"NaN\"").Number() is null, "Reject Nonfinite Values");
    Check(new Metric("Usage", 25, 100, "usd").Percent == 25, "Dollar Utilization");
    var legacy = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
    {
        Schema = 1, Accounts = store.State.Accounts, Pins = store.State.Pins,
        Hidden = store.State.Hidden, MetricOrder = store.State.MetricOrder, RetryAfter = store.State.RetryAfter,
        Notifications = store.State.Notifications
    });
    File.WriteAllBytes(Path.Combine(directory, "accounts.dat"), ProtectedData.Protect(legacy, null, DataProtectionScope.CurrentUser));
    var upgraded = new AccountStore(directory);
    Check(upgraded.State.Display.IsValid && upgraded.State.Accounts.Count == store.State.Accounts.Count, "Existing Account Files Must Acquire Display Defaults Without Losing Accounts");
    Check(upgraded.State.Accounts.First(x => x.Id == c.Id).Credential.RefreshToken == "rotated-refresh", "Display Upgrade Must Preserve Saved Tokens");
    File.WriteAllBytes(Path.Combine(directory, "accounts.dat"), [1, 2, 3]);
    try { _ = new AccountStore(directory); throw new Exception("Corruption Silently Accepted"); } catch (CryptographicException) { }
    if (args.Length == 2 && args[0] == "--window") { UiChecks.WindowSmoke(directory, args[1]); return; }
    LoginChecks.Run();
    UsageChecks.Run();
    var uiDirectory = Path.Combine(directory, "ui");
    Directory.CreateDirectory(uiDirectory);
    UiChecks.Run(uiDirectory, args.Length == 2 && args[0] == "--render" ? args[1] : null);
    foreach (var file in Directory.EnumerateFiles(uiDirectory)) File.Delete(file);
    Directory.Delete(uiDirectory);
    Console.WriteLine("PASS: Storage, Ordering, Credential Protection, OAuth, Usage And Rendered UI Regression Checks");
}
finally
{
    // This directory is unique to this test invocation and contains no user data.
    Directory.Delete(directory, recursive: true);
}
