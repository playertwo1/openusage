namespace OpenUsage.Windows;

public enum ProviderKind { Claude, Codex, Cursor, Antigravity, Copilot, Grok, Ollama, OpenCode, OpenRouter, Zai }

public static class Providers
{
    public static string Name(this ProviderKind kind) => kind switch
    {
        ProviderKind.Codex => "ChatGPT", ProviderKind.Zai => "Z.ai", _ => kind.ToString()
    };
    public static bool UsesApiKey(this ProviderKind kind) => kind is ProviderKind.Ollama or ProviderKind.OpenCode or ProviderKind.OpenRouter or ProviderKind.Zai;
    public static string? KeyPage(this ProviderKind kind) => kind switch
    {
        ProviderKind.Ollama => "https://ollama.com/settings/keys",
        ProviderKind.OpenCode => "https://opencode.ai/console",
        ProviderKind.OpenRouter => "https://openrouter.ai/settings/management-keys",
        ProviderKind.Zai => "https://z.ai/manage-apikey/apikey-list", _ => null
    };
    // Keep the Swift descriptor defaults; these also determine the tray summary.
    public static bool Pinned(this ProviderKind kind, string label) => kind switch
    {
        ProviderKind.Claude or ProviderKind.Codex => label is "Session" or "Weekly",
        ProviderKind.Cursor => label is "Cursor Models" or "Other Models",
        ProviderKind.Antigravity => label is "Gemini 5h" or "Gemini",
        ProviderKind.Copilot => label == "Credits", ProviderKind.Grok => label == "Weekly",
        ProviderKind.Ollama => label == "Monthly", ProviderKind.OpenCode => label == "Session",
        ProviderKind.OpenRouter => label == "Balance", ProviderKind.Zai => label is "Session" or "Weekly", _ => false
    };
}

public sealed record Credential(string AccessToken, string? RefreshToken = null, DateTimeOffset? ExpiresAt = null,
    Dictionary<string, string>? Extras = null);
public sealed record Account(Guid Id, ProviderKind Kind, string Label, Credential Credential);
public sealed record Metric(string Label, double Value, double? Limit = null, string Unit = "percent", DateTimeOffset? ResetsAt = null, string? Text = null)
{
    public double? Percent => Limit is > 0 ? Math.Clamp(Value / Limit.Value * 100, 0, 100) : Unit == "percent" ? Math.Clamp(Value, 0, 100) : null;
    public string Display => Text ?? (Unit == "percent" ? $"{Value:0.#}% Used" : Limit.HasValue ? $"{Format(Value)} / {Format(Limit.Value)}" : Format(Value));
    private string Format(double value) => Unit == "usd" ? $"${value:0.00}" : $"{value:0.##}{(Unit is "count" or "percent" ? "" : " " + Unit)}";
}
public sealed record UsageSnapshot(string? Plan, IReadOnlyList<Metric> Metrics);
public sealed record LoginResult(string Label, Credential Credential);
public sealed record LoginPrompt(string Message, string? Url = null, string? Code = null, bool AcceptsPastedCode = false);

public sealed class ProviderException(string message, int? statusCode = null, DateTimeOffset? retryAfter = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
    public DateTimeOffset? RetryAfter { get; } = retryAfter;
}
