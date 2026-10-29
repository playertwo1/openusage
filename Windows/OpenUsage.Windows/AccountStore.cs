using System.Security.Cryptography;
using System.Text.Json;

namespace OpenUsage.Windows;

public sealed class AppState
{
    public int Schema { get; set; } = 1;
    public List<Account> Accounts { get; set; } = [];
    public Dictionary<string, bool> Pins { get; set; } = [];
    public HashSet<string> Hidden { get; set; } = [];
    public Dictionary<Guid, List<string>> MetricOrder { get; set; } = [];
    public Dictionary<Guid, DateTimeOffset> RetryAfter { get; set; } = [];
    public bool Notifications { get; set; }
    public DisplayPreferences Display { get; set; } = new();
    public Dictionary<Guid, List<WeekSample>> History { get; set; } = [];
}

public sealed record WeekSample(DateTimeOffset At, double Percent);

public sealed class AccountStore
{
    public static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenUsage.Windows");
    private readonly string path;
    public AppState State { get; private set; }
    public AccountStore(string? directory = null)
    {
        path = Path.Combine(directory ?? DataDirectory, "accounts.dat");
        State = File.Exists(path) ? Decode(File.ReadAllBytes(path)) : new AppState();
    }

    private static AppState Decode(byte[] bytes)
    {
        var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        try
        {
            var state = JsonSerializer.Deserialize<AppState>(plain) ?? throw new InvalidDataException("The Account File Is Empty.");
            Validate(state);
            return state;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static void Validate(AppState state)
    {
        if (state.Schema != 1 || state.Accounts is null || state.Pins is null || state.Hidden is null || state.MetricOrder is null || state.RetryAfter is null
            || state.History is null || state.Display is null || !state.Display.IsValid
            || state.Accounts.Select(a => a.Id).Distinct().Count() != state.Accounts.Count
            || state.Accounts.Any(a => a.Id == Guid.Empty || !Enum.IsDefined(a.Kind) || string.IsNullOrWhiteSpace(a.Label)
                || a.Credential is null || !ValidSecret(a.Credential.AccessToken)))
            throw new InvalidDataException("The Account File Has An Unsupported Or Invalid Format.");
    }

    public static bool ValidSecret(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 32768 && !value.Any(char.IsControl);
    public static string MetricKey(Guid id, string label) => $"{id:N}/{label}";
    public bool IsPinned(Account account, Metric metric) => State.Pins.GetValueOrDefault(MetricKey(account.Id, metric.Label), account.Kind.Pinned(metric.Label));
    public bool IsHidden(Account account, Metric metric) => State.Hidden.Contains(MetricKey(account.Id, metric.Label));

    public void Edit(Action<AppState> edit)
    {
        var next = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(State))!;
        edit(next);
        Validate(next);
        var plain = JsonSerializer.SerializeToUtf8Bytes(next);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(encrypted);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            State = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Add(Account account) => Edit(state =>
    {
        var index = state.Accounts.FindLastIndex(a => a.Kind == account.Kind);
        state.Accounts.Insert(index < 0 ? state.Accounts.Count : index + 1, account);
    });
    public void Replace(Account account) => Edit(state =>
    {
        var index = state.Accounts.FindIndex(a => a.Id == account.Id);
        if (index < 0) throw new InvalidOperationException("This Account Has Been Removed.");
        state.Accounts[index] = account;
    });
    public void Rename(Guid id, string label) => Edit(state =>
    {
        var index = state.Accounts.FindIndex(a => a.Id == id);
        if (index < 0) throw new InvalidOperationException("This Account Has Been Removed.");
        state.Accounts[index] = state.Accounts[index] with { Label = label };
    });
    public void Remove(Guid id) => Edit(state =>
    {
        state.Accounts.RemoveAll(a => a.Id == id);
        foreach (var key in state.Pins.Keys.Where(k => k.StartsWith($"{id:N}/", StringComparison.Ordinal)).ToList()) state.Pins.Remove(key);
        state.Hidden.RemoveWhere(k => k.StartsWith($"{id:N}/", StringComparison.Ordinal));
        state.MetricOrder.Remove(id);
        state.RetryAfter.Remove(id);
        state.History.Remove(id);
    });
    public void MoveAccount(Guid id, int direction) => Edit(state =>
    {
        var index = state.Accounts.FindIndex(a => a.Id == id);
        if (index < 0) return;
        var other = index + direction;
        if (other >= 0 && other < state.Accounts.Count && state.Accounts[other].Kind == state.Accounts[index].Kind)
            (state.Accounts[index], state.Accounts[other]) = (state.Accounts[other], state.Accounts[index]);
    });
    public void MoveProvider(ProviderKind kind, int direction) => Edit(state =>
    {
        var kinds = state.Accounts.Select(a => a.Kind).Distinct().ToList();
        var index = kinds.IndexOf(kind);
        var other = index + direction;
        if (index < 0 || other < 0 || other >= kinds.Count) return;
        (kinds[index], kinds[other]) = (kinds[other], kinds[index]);
        state.Accounts = kinds.SelectMany(k => state.Accounts.Where(a => a.Kind == k)).ToList();
    });
    // Weekly readings as the provider reported them, kept only for the current week so daily use can be shown.
    public void RecordWeekly(Account account, UsageSnapshot snapshot, DateTimeOffset now)
    {
        if (MetricPresentation.Window(snapshot.Metrics, true, m => m.ResetsAt.HasValue) is not { } weekly) return;
        var start = weekly.ResetsAt!.Value.AddDays(-7);
        var current = State.History.GetValueOrDefault(account.Id)?.Where(s => s.At >= start).ToList() ?? [];
        var last = current.LastOrDefault();
        if (last is not null && last.Percent == weekly.Percent && now - last.At < TimeSpan.FromHours(6) && current.Count == State.History[account.Id].Count) return;
        Edit(state => state.History[account.Id] = [.. current, new WeekSample(now, weekly.Percent!.Value)]);
    }
    public IEnumerable<Metric> Ordered(Account account, UsageSnapshot snapshot)
    {
        var saved = State.MetricOrder.GetValueOrDefault(account.Id) ?? [];
        return snapshot.Metrics.OrderBy(m => { var index = saved.IndexOf(m.Label); return index < 0 ? int.MaxValue : index; });
    }
    public void MoveMetric(Account account, UsageSnapshot snapshot, string label, int direction) => Edit(state =>
    {
        var labels = Ordered(account, snapshot).Select(m => m.Label).ToList();
        var index = labels.IndexOf(label);
        var other = index + direction;
        if (index >= 0 && other >= 0 && other < labels.Count) (labels[index], labels[other]) = (labels[other], labels[index]);
        state.MetricOrder[account.Id] = labels;
    });
}
