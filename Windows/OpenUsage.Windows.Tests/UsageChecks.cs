using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public static class UsageChecks
{
    private const string ClaudeFixture = """
        {"five_hour":{"utilization":15,"resets_at":1800000000},"seven_day":{"utilization":35},
         "seven_day_sonnet":{"utilization":18},"limits":[{"kind":"weekly_scoped","scope":{"model":{"display_name":"Fable"}},"percent":23}],
         "extra_usage":{"is_enabled":true,"used_credits":1234,"monthly_limit":10000}}
        """;
    private const string CodexFixture = """
        {"plan_type":"prolite","rate_limit":{"primary_window":{"used_percent":80,"limit_window_seconds":604800},
         "secondary_window":{"used_percent":20,"limit_window_seconds":18000}},
         "additional_rate_limits":[{"limit_name":"codex-spark","rate_limit":{"primary_window":{"used_percent":7}}}],
         "credits":{"balance":"99.9"},"rate_limit_reset_credits":{"available_count":2}}
        """;
    private const string CursorFixture = """
        {"enabled":true,"billingCycleEnd":1800000000000,"planUsage":{"limit":2000,"totalSpend":500,
         "totalPercentUsed":25,"autoPercentUsed":12,"apiPercentUsed":13},
         "spendLimitUsage":{"individualLimit":10000,"individualRemaining":9000}}
        """;
    private const string AntigravityFixture = """
        {"groups":[{"displayName":"Gemini","buckets":[{"bucketId":"gemini-5h","remainingFraction":0.8},
         {"bucketId":"gemini-weekly","remainingFraction":0.6},{"bucketId":"3p-5h","remainingFraction":0.7},
         {"bucketId":"3p-weekly","remainingFraction":0.4}]},{"displayName":"Gemini 4 Argon",
         "buckets":[{"bucketId":"argon-weekly","window":"weekly","remainingFraction":0.9}]}]}
        """;
    private const string CopilotFixture = """
        {"copilot_plan":"individual_pro","quota_reset_date":"2027-01-01","quota_snapshots":{
         "premium_interactions":{"entitlement":300,"remaining":225,"overage_permitted":true,"overage_count":3},
         "chat":{"unlimited":true},"completions":{"entitlement":1000,"remaining":500}}}
        """;
    private const string GrokFixture = """
        {"config":{"currentPeriod":{"type":"USAGE_PERIOD_TYPE_WEEKLY","start":"2026-10-01T00:00:00Z","end":"2026-10-08T00:00:00Z"},
         "creditUsagePercent":45,"onDemandCap":{"val":"100"}}}
        """;
    private const string OllamaFixture = """
        {"included":{"session":{"remaining_percent":80},"weekly":{"remaining_percent":60},"allowance_usd":20,
         "balance_usd":15,"period":{"until":"2027-01-01T00:00:00Z"}},"purchased":{"balance_usd":12.5}}
        """;
    private const string OpenCodeFixture = """
        {"usage":{"rolling":{"percent":10,"resetsAt":"2027-01-01T00:00:00Z"},"weekly":{"percent":20,"resetsAt":"2027-01-02T00:00:00Z"},
         "monthly":{"percent":30,"resetsAt":"2027-01-03T00:00:00Z"}}}
        """;
    private const string ZaiFixture = """
        {"data":{"limits":[{"type":"CREDIT_LIMIT","unit":6,"number":1,"percentage":30,"nextResetTime":1800000000000},
         {"name":"TOKENS_LIMIT","unit":3,"number":5,"percentage":10},{"type":"TIME_LIMIT","currentValue":5,"usage":100}]}}
        """;
    private static JsonNode J(string text) => JsonNode.Parse(text)!;
    private static Metric Row(UsageSnapshot usage, string label) => usage.Metrics.Single(m => m.Label == label);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("Usage: " + message); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (ProviderException) { return; }
        throw new Exception("Usage: Accepted " + message);
    }

    public static void Run()
    {
        var claude = UsageMappers.Claude(J(ClaudeFixture));
        Check(claude.Metrics.Select(m => m.Label).SequenceEqual(new[] { "Session", "Weekly", "Sonnet", "Fable", "Extra Usage" }), "Claude metric declaration order");
        Check(Row(claude, "Extra Usage").Value == 12.34 && Row(claude, "Extra Usage").Limit == 100 && Row(claude, "Extra Usage").Unit == "usd", "Claude cents to dollars");
        var codex = UsageMappers.Codex(J(CodexFixture));
        Check(codex.Plan == "Pro 100" && Row(codex, "Session").Value == 20 && Row(codex, "Weekly").Value == 80, "Codex windows classified by duration");
        Check(Row(codex, "Extra Usage").Value == 99 && Row(codex, "Extra Usage").Unit == "credits", "Codex credits floor");
        Check(codex.Metrics.Last().Label == "Limit Resets", "Codex descriptor order");
        var cursor = UsageMappers.Cursor(J(CursorFixture));
        Check(Row(cursor, "Total Usage").Value == 25 && Row(cursor, "Extra Usage").Value == 10 && Row(cursor, "Extra Usage").Limit == 100, "Cursor percentages and provider reported spend");
        var team = UsageMappers.Cursor(J("""{"enabled":true,"planUsage":{"limit":2000,"totalSpend":500,"autoPercentUsed":10,"apiPercentUsed":20},"spendLimitUsage":{"limitType":"team"}}"""));
        Check(!team.Metrics.Any(m => m.Label == "Total Usage"), "Cursor team model pools cannot synthesize stale dollar total");
        var fallback = UsageMappers.Cursor(J("{}"), summary: J("""{"membershipType":"enterprise","individualUsage":{"onDemand":{"enabled":false}},"teamUsage":{"onDemand":{"limit":200000,"used":12345}}}"""));
        Check(fallback.Plan == "Enterprise" && Row(fallback, "Extra Usage").Value == 123.45 && Row(fallback, "Extra Usage").Limit == 2000, "Cursor enterprise on-demand summary");
        var requests = UsageMappers.Cursor(J("{}"), requests: J("""{"gpt-4":{"maxRequestUsage":500,"numRequests":25}}"""));
        Check(Row(requests, "Requests").Value == 25 && Row(requests, "Requests").Limit == 500 && Row(requests, "Requests").Unit == "requests", "Cursor request-based fallback");
        var antigravity = UsageMappers.Antigravity([J(AntigravityFixture)]);
        Check(antigravity.Metrics.Select(m => m.Label).SequenceEqual(new[] { "Gemini 5h", "Gemini", "Argon", "Claude 5h", "Claude Weekly" }), "Antigravity order including Argon");
        Check(Row(antigravity, "Gemini 5h").Value == 20 && Row(antigravity, "Argon").Value == 10 && UsageMappers.CompleteAntigravity(J(AntigravityFixture)), "Antigravity remaining fraction");
        var ambiguous = J(AntigravityFixture);
        ((JsonArray)ambiguous["groups"]![1]!["buckets"]!).Add(J("""{"bucketId":"argon-2","window":"weekly","remainingFraction":0.1}"""));
        Check(!UsageMappers.Antigravity([ambiguous]).Metrics.Any(m => m.Label == "Argon"), "Ambiguous Argon bucket suppressed");
        var copilot = UsageMappers.Copilot(J(CopilotFixture));
        Check(Row(copilot, "Credits").Value == 25 && Row(copilot, "Extra Usage").Value == 3 && !copilot.Metrics.Any(m => m.Label == "Chat"), "Copilot unlimited suppression and overage");
        var personal = UsageMappers.Copilot(J("""{"token_based_billing":true,"quota_snapshots":{"premium_interactions":{"credits_used":12}}}"""));
        Check(Row(personal, "Credits").Value == 12 && Row(personal, "Credits").Unit == "credits", "Copilot personal token billing");
        var grok = UsageMappers.Grok(J(GrokFixture));
        Check(Row(grok, "Weekly").Value == 45 && Row(grok, "Extra Usage").Text == "100 Cap", "Grok percent and cap badge");
        var grokZero = J(GrokFixture); ((JsonObject)grokZero["config"]!).Remove("creditUsagePercent");
        Check(Row(UsageMappers.Grok(grokZero), "Weekly").Value == 0, "Grok proto-JSON omitted zero");
        var ollama = UsageMappers.Ollama(J(OllamaFixture));
        Check(Row(ollama, "Session").Value == 20 && Row(ollama, "Monthly").Value == 5 && Row(ollama, "Monthly").Limit == 20 && Row(ollama, "Monthly").Unit == "usd", "Ollama balances");
        var opencode = UsageMappers.OpenCode(J(OpenCodeFixture));
        Check(Row(opencode, "Monthly").Value == 30 && opencode.Plan == "Go", "OpenCode windows");
        var placeholder = J(OpenCodeFixture); placeholder["usage"]!["rolling"]!["percent"] = 0d; placeholder["usage"]!["rolling"]!["resetsAt"] = "2026-10-29T05:00:00Z";
        Check(Row(UsageMappers.OpenCode(placeholder, DateTimeOffset.Parse("2026-10-29T00:00:00Z")), "Session").ResetsAt is null, "OpenCode unstarted rolling reset");
        var router = UsageMappers.OpenRouter(J("""{"data":{"total_credits":100,"total_usage":25}}"""), J("""{"data":{"usage_daily":2,"usage_weekly":8,"usage_monthly":25}}"""));
        Check(Row(router, "Balance").Value == 75 && Row(router, "Today").Unit == "usd", "OpenRouter provider dollars");
        var zai = UsageMappers.Zai(J(ZaiFixture));
        Check(zai.Metrics.Select(m => m.Label).SequenceEqual(new[] { "Session", "Weekly", "Web Searches" }) && Row(zai, "Web Searches").Value == 5 && Row(zai, "Web Searches").Limit == 100, "Zai order, window units, searches");
        foreach (var map in new Func<JsonNode, UsageSnapshot>[] { n => UsageMappers.Claude(n), n => UsageMappers.Codex(n), n => UsageMappers.Copilot(n), n => UsageMappers.Ollama(n), n => UsageMappers.OpenCode(n), n => UsageMappers.Zai(n), n => UsageMappers.Grok(n), n => UsageMappers.Cursor(n) })
            Reject(() => map(J("[]")), "non-object provider response");
        Reject(() => UsageMappers.Grok(J(GrokFixture.Replace("45", "\"NaN\""))), "Grok nonnumeric percentage");
        Reject(() => UsageMappers.Grok(J(GrokFixture.Replace("45", "null"))), "Grok explicit null is not an omitted proto-JSON zero");
        Reject(() => UsageMappers.Grok(J(GrokFixture.Replace("\"100\"", "null"))), "Grok explicit null cap");
        Reject(() => UsageMappers.OpenCode(J("""{"usage":{"rolling":{"percent":20}}}""")), "OpenCode incomplete windows");
        Reject(() => UsageMappers.Zai(J("""{"limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5}]}""")), "Zai missing percent");
        Reject(() => UsageMappers.Antigravity([J("{}")]), "Antigravity no recognized quotas");
        FetchChecks().GetAwaiter().GetResult();
    }

    private static async Task FetchChecks()
    {
        var fixtures = new Dictionary<string, string>
        {
            ["/api/oauth/usage"] = ClaudeFixture,
            ["/backend-api/wham/usage"] = CodexFixture,
            ["/aiserver.v1.DashboardService/GetCurrentPeriodUsage"] = CursorFixture,
            ["/v1internal:retrieveUserQuotaSummary"] = AntigravityFixture,
            ["/copilot_internal/user"] = CopilotFixture,
            ["/v1/billing"] = GrokFixture,
            ["/api/balance"] = OllamaFixture,
            ["/zen/go/v1/usage"] = OpenCodeFixture,
            ["/api/v1/credits"] = """{"data":{"total_credits":100,"total_usage":25}}""",
            ["/api/v1/key"] = """{"data":{"usage_daily":2}}""",
            ["/api/monitor/usage/quota/limit"] = ZaiFixture
        };
        string? copilotAuth = null, claudeAgent = null, codexAccount = null;
        var counts = new Dictionary<string, int>();
        using var api = new Api(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            counts[path] = counts.GetValueOrDefault(path) + 1;
            if (path == "/copilot_internal/user") copilotAuth = request.Headers.Authorization?.ToString();
            if (path == "/api/oauth/usage") claudeAgent = request.Headers.UserAgent.ToString();
            if (path == "/backend-api/wham/usage") codexAccount = request.Headers.GetValues("ChatGPT-Account-Id").Single();
            return new(HttpStatusCode.OK) { Content = new StringContent(fixtures.GetValueOrDefault(path) ?? "{}") };
        }));
        var service = new UsageService(api);
        foreach (var kind in Enum.GetValues<ProviderKind>())
        {
            var result = await service.FetchAsync(kind, new Credential("test-token", Extras: new() { ["chatgpt_account_id"] = "test-account" }));
            Check(result.Metrics.Count > 0, "Fetch " + kind);
        }
        Check(!counts.ContainsKey("/api/oauth/profile"), "Claude secondary reads must not share the usage burst");
        await service.RefreshSecondaryAsync(ProviderKind.Claude, new Credential("test-token"));
        await service.RefreshSecondaryAsync(ProviderKind.Claude, new Credential("test-token"));
        Check(counts["/api/oauth/profile"] == 1, "Secondary plan/profile reads hourly");
        Check(copilotAuth == "token test-token", "Copilot token scheme");
        Check(claudeAgent == "claude-cli/2.1.294 (external, cli)", "Claude exact user-agent");
        Check(codexAccount == "test-account", "Codex account identity header");
        var sessionToken = "x." + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"sub\":\"auth0|user-id\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".x";
        Check(UsageService.CursorSession(sessionToken)?.User == "user-id" && UsageService.CursorSession("invalid") is null, "Cursor session belongs only to this account token");
        using var gated = new Api(new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/credits")
            ? new(HttpStatusCode.Forbidden) : new(HttpStatusCode.OK) { Content = new StringContent("""{"data":{"usage_daily":12}}""") }));
        var partial = await new UsageService(gated).FetchAsync(ProviderKind.OpenRouter, new Credential("test-key"));
        Check(Row(partial, "Today").Value == 12, "One forbidden OpenRouter endpoint keeps other endpoint usable");
        var limitedCalls = 0;
        using var limited = new Api(new Handler(_ => { limitedCalls++; return new(HttpStatusCode.TooManyRequests); }));
        var limitedService = new UsageService(limited);
        DateTimeOffset? until = null;
        for (var i = 0; i < 2; i++)
        {
            try { await limitedService.FetchAsync(ProviderKind.Claude, new Credential("limited-test-token")); throw new Exception("Rate Limit Accepted"); }
            catch (ProviderException error) when (error.StatusCode == 429)
            {
                Check(error.RetryAfter > DateTimeOffset.UtcNow.AddMinutes(59), "Claude rate-limit deadline available for persisted backoff");
                until ??= error.RetryAfter;
                Check(error.RetryAfter == until, "Blocked calls preserve original deadline");
            }
        }
        Check(limitedCalls == 1, "Claude does not send requests inside rate-limit window");
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(reply(request));
    }
}
