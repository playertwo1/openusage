using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public sealed class UsageService(Api api)
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, JsonNode? Body)> secondary = new();
    private readonly ConcurrentDictionary<string, JsonNode> claudeUsage = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> claudeBackoff = new();
    private static readonly Dictionary<string, string> ClaudeHeaders = new()
    {
        ["anthropic-beta"] = "oauth-2025-04-20", ["User-Agent"] = "claude-cli/2.1.294 (external, cli)"
    };
    private static readonly Dictionary<string, string> ConnectHeaders = new() { ["Connect-Protocol-Version"] = "1" };

    public async Task<UsageSnapshot> FetchAsync(ProviderKind kind, Credential credential, CancellationToken ct = default)
    {
        var token = credential.AccessToken;
        if (string.IsNullOrWhiteSpace(token)) throw new ProviderException("Sign In To This Account Again.");
        return kind switch
        {
            ProviderKind.Claude => await Claude(credential, ct),
            ProviderKind.Codex => await Codex(credential, ct),
            ProviderKind.Cursor => await Cursor(token, ct),
            ProviderKind.Antigravity => await Antigravity(token, ct),
            ProviderKind.Copilot => UsageMappers.Copilot(await api.GetAsync("https://api.github.com/copilot_internal/user", headers: new()
            {
                ["Authorization"] = "token " + token, ["Editor-Version"] = "vscode/1.96.2",
                ["Editor-Plugin-Version"] = "copilot-chat/0.26.7", ["User-Agent"] = "GitHubCopilotChat/0.26.7",
                ["X-Github-Api-Version"] = "2025-04-01"
            }, ct: ct)),
            ProviderKind.Grok => await Grok(token, ct),
            ProviderKind.Ollama => UsageMappers.Ollama(await api.GetAsync("https://ollama.com/api/balance", token, ct: ct),
                await Hourly(kind, token, "plan", () => api.SendAsync(HttpMethod.Post, "https://ollama.com/api/me", token, ct: ct), ct)),
            ProviderKind.OpenCode => UsageMappers.OpenCode(await api.GetAsync("https://opencode.ai/zen/go/v1/usage", token, ct: ct)),
            ProviderKind.OpenRouter => await OpenRouter(token, ct),
            ProviderKind.Zai => UsageMappers.Zai(await api.GetAsync("https://api.z.ai/api/monitor/usage/quota/limit", token, ct: ct),
                await Hourly(kind, token, "plan", () => api.GetAsync("https://api.z.ai/api/biz/subscription/list", token, ct: ct), ct)),
            _ => throw new ProviderException("This Provider Is Not Supported.")
        };
    }

    private async Task<UsageSnapshot> Claude(Credential credential, CancellationToken ct)
    {
        var token = credential.AccessToken;
        var usage = await ClaudeRequest(credential, () => api.GetAsync("https://api.anthropic.com/api/oauth/usage", token, ClaudeHeaders, ct));
        claudeUsage[ClaudeKey(credential)] = usage;
        secondary.TryGetValue(CacheKey(ProviderKind.Claude, token, "profile"), out var profile);
        secondary.TryGetValue(CacheKey(ProviderKind.Claude, token, "resets"), out var grants);
        return UsageMappers.Claude(usage, profile.Body, grants.Body);
    }

    // The caller runs this halfway between usage passes; a grants read in the same burst triggers 429.
    public async Task<UsageSnapshot?> RefreshSecondaryAsync(ProviderKind kind, Credential credential, CancellationToken ct = default)
    {
        if (kind != ProviderKind.Claude || !claudeUsage.TryGetValue(ClaudeKey(credential), out var usage)) return null;
        var token = credential.AccessToken;
        var profile = await Hourly(kind, token, "profile", () => ClaudeRequest(credential,
            () => api.GetAsync("https://api.anthropic.com/api/oauth/profile", token, ClaudeHeaders, ct)), ct, true);
        var grants = await Hourly(kind, token, "resets", () => ClaudeRequest(credential,
            () => api.GetAsync("https://api.anthropic.com/api/oauth/usage?cedar_ember=1&skip_spend=1", token, ClaudeHeaders, ct)), ct, true);
        return UsageMappers.Claude(usage, profile, grants);
    }

    private static string ClaudeKey(Credential credential) => credential.Extras?.GetValueOrDefault("account_identity")
        ?? CacheKey(ProviderKind.Claude, credential.AccessToken, "identity");

    private async Task<JsonNode> ClaudeRequest(Credential credential, Func<Task<JsonNode>> request)
    {
        var key = ClaudeKey(credential);
        if (claudeBackoff.TryGetValue(key, out var until) && until > DateTimeOffset.UtcNow)
            throw new ProviderException("Claude Is Rate Limiting This Account. Waiting Before Trying Again.", 429, until);
        try { return await request(); }
        catch (ProviderException error) when (error.StatusCode == 429)
        {
            until = DateTimeOffset.UtcNow.AddHours(1);
            claudeBackoff[key] = until;
            throw new ProviderException(error.Message, 429, until);
        }
    }

    private async Task<UsageSnapshot> Codex(Credential credential, CancellationToken ct)
    {
        Dictionary<string, string>? headers = null;
        if (credential.Extras?.GetValueOrDefault("chatgpt_account_id") is { Length: > 0 } id)
            headers = new() { ["ChatGPT-Account-Id"] = id };
        return UsageMappers.Codex(await api.GetAsync("https://chatgpt.com/backend-api/wham/usage", credential.AccessToken, headers, ct));
    }

    private async Task<UsageSnapshot> Grok(string token, CancellationToken ct)
    {
        var headers = new Dictionary<string, string> { ["X-XAI-Token-Auth"] = "xai-grok-cli" };
        var usage = await api.GetAsync("https://cli-chat-proxy.grok.com/v1/billing?format=credits", token, headers, ct);
        var settings = await Hourly(ProviderKind.Grok, token, "plan", () => api.GetAsync("https://cli-chat-proxy.grok.com/v1/settings", token, headers, ct), ct);
        return UsageMappers.Grok(usage, settings);
    }

    private async Task<UsageSnapshot> OpenRouter(string token, CancellationToken ct)
    {
        // Endpoint permissions differ by key type: one refusal must not hide the other endpoint's data.
        JsonNode? credits = null, key = null;
        Exception? first = null;
        try { credits = await api.GetAsync("https://openrouter.ai/api/v1/credits", token, ct: ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is ProviderException or HttpRequestException or TaskCanceledException) { first = ex; }
        try { key = await api.GetAsync("https://openrouter.ai/api/v1/key", token, ct: ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is ProviderException or HttpRequestException or TaskCanceledException) { first ??= ex; }
        if (credits is null && key is null) throw first ?? Json.Invalid();
        var result = UsageMappers.OpenRouter(credits, key);
        if (result.Metrics.Count == 0) throw first ?? Json.Invalid();
        return result;
    }

    private async Task<UsageSnapshot> Cursor(string token, CancellationToken ct)
    {
        Task<JsonNode> Connect(string method) => api.SendAsync(HttpMethod.Post,
            "https://api2.cursor.sh/aiserver.v1.DashboardService/" + method, token, new { }, ConnectHeaders, ct);
        var usage = await Connect("GetCurrentPeriodUsage");
        if (usage is not JsonObject) throw Json.Invalid();
        if (usage["planUsage"] is { } rawPlan && rawPlan is not JsonObject) throw Json.Invalid();
        var plan = await Hourly(ProviderKind.Cursor, token, "plan", () => Connect("GetPlanInfo"), ct);
        var credits = await Hourly(ProviderKind.Cursor, token, "credits", () => Connect("GetCreditGrantsBalance"), ct);
        JsonNode? summary = null, requests = null, stripe = null;
        if (CursorSession(token) is { } session)
        {
            var headers = new Dictionary<string, string> { ["Cookie"] = "WorkosCursorSessionToken=" + session.Session };
            summary = await Optional("Cursor Summary", () => api.GetAsync("https://cursor.com/api/usage-summary", headers: headers, ct: ct), ct);
            if (usage["planUsage"]?["limit"].Number() is null && usage["planUsage"]?["totalPercentUsed"].Number() is null)
                requests = await Optional("Cursor Requests", () => api.GetAsync("https://cursor.com/api/usage?user=" + Uri.EscapeDataString(session.User), headers: headers, ct: ct), ct);
            stripe = await Hourly(ProviderKind.Cursor, token, "stripe", () => api.GetAsync("https://cursor.com/api/auth/stripe", headers: headers, ct: ct), ct);
        }
        var grokBot = await Optional("Cursor Grok Bot", () => Connect("GetSandUsageStatus"), ct);
        return UsageMappers.Cursor(usage, plan, credits, stripe, summary, requests, grokBot);
    }

    public static (string User, string Session)? CursorSession(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            var subject = JsonNode.Parse(Convert.FromBase64String(payload))?["sub"].Text()?.Trim();
            if (string.IsNullOrEmpty(subject)) return null;
            var subjectParts = subject.Split('|');
            var user = subjectParts.Length > 1 ? subjectParts[1] : subjectParts[0];
            return string.IsNullOrEmpty(user) ? null : (user, user + "%3A%3A" + token);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException) { return null; }
    }

    private async Task<UsageSnapshot> Antigravity(string token, CancellationToken ct)
    {
        var headers = new Dictionary<string, string> { ["User-Agent"] = "antigravity/2.24.0 (aidev_client; os_type=windows; arch=amd64)" };
        var bases = new[] { "https://cloudcode-pa.googleapis.com", "https://daily-cloudcode-pa.googleapis.com" };
        var assist = await Hourly(ProviderKind.Antigravity, token, "plan", async () =>
        {
            Exception? failure = null;
            foreach (var host in bases)
            {
                try
                {
                    return await api.SendAsync(HttpMethod.Post, host + "/v1internal:loadCodeAssist", token,
                        new { metadata = new { ideType = "ANTIGRAVITY", platform = "PLATFORM_UNSPECIFIED", pluginType = "GEMINI" } }, headers, ct);
                }
                catch (ProviderException ex) when (ex.StatusCode is not 401 and not 403) { failure = ex; }
                catch (HttpRequestException ex) { failure = ex; }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) { failure = ex; }
            }
            throw failure ?? Json.Invalid();
        }, ct);
        var bodies = new List<JsonNode>();
        Exception? error = null;
        foreach (var host in bases)
        {
            try
            {
                object body = (assist as JsonObject)?["cloudaicompanionProject"].Text() is { Length: > 0 } project ? new { project } : new { };
                var summary = await api.SendAsync(HttpMethod.Post, host + "/v1internal:retrieveUserQuotaSummary", token, body, headers, ct);
                bodies.Add(summary);
                if (UsageMappers.CompleteAntigravity(summary)) break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is ProviderException or HttpRequestException or TaskCanceledException)
            { if (error is not ProviderException { StatusCode: 401 or 403 }) error = ex; }
        }
        if (bodies.Count == 0) throw error ?? Json.Invalid();
        return UsageMappers.Antigravity(bodies, assist);
    }

    private static string CacheKey(ProviderKind kind, string token, string name) => kind + ":" + name + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<JsonNode?> Hourly(ProviderKind kind, string token, string name, Func<Task<JsonNode>> request, CancellationToken ct, bool propagateRateLimit = false)
    {
        var key = CacheKey(kind, token, name);
        if (secondary.TryGetValue(key, out var entry) && DateTimeOffset.UtcNow - entry.At < TimeSpan.FromHours(1)) return entry.Body;
        var body = await Optional(kind + " " + name, request, ct, propagateRateLimit);
        secondary[key] = (DateTimeOffset.UtcNow, body);
        return body;
    }

    private static async Task<JsonNode?> Optional(string operation, Func<Task<JsonNode>> request, CancellationToken ct, bool propagateRateLimit = false)
    {
        try { return await request(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (ProviderException error) when (propagateRateLimit && error.StatusCode == 429) { throw; }
        catch (Exception ex) when (ex is ProviderException or HttpRequestException or TaskCanceledException)
        {
            AppLog.Error("Optional " + operation, ex);
            return null;
        }
    }
}
