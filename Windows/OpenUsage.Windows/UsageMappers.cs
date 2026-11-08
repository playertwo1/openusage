using System.Globalization;
using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public static partial class UsageMappers
{
    private static JsonNode? F(JsonNode? node, string key) => (node as JsonObject)?[key];
    private static IEnumerable<JsonNode> Items(JsonNode? node) => node is JsonArray array ? array.OfType<JsonNode>() : [];
    private static JsonNode Object(JsonNode node) => node is JsonObject ? node : throw Json.Invalid();
    private static bool? LooseBool(JsonNode? node) => node.Bool() ?? node.Text()?.Trim().ToLowerInvariant() switch
    {
        "true" or "1" => true, "false" or "0" => false, _ => node.Number() is double n ? n != 0 : null
    };
    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
    private static Metric Percent(string label, double value, DateTimeOffset? reset = null, double? hours = null) => new(label, Clamp(value), ResetsAt: reset, WindowHours: hours);
    private static string? Title(string? text) => string.IsNullOrWhiteSpace(text) ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.Trim().Replace('_', ' ').Replace('-', ' ').ToLowerInvariant());
    private static DateTimeOffset? Milliseconds(JsonNode? node) => node.Number() is double value ? Epoch(value / 1000) : node.Date();
    private static DateTimeOffset? Epoch(double value)
    {
        try { return DateTimeOffset.FromUnixTimeSeconds(checked((long)value)); }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException) { return null; }
    }
    private static DateTimeOffset? FlexibleDate(JsonNode? node) => node.Number() is double n ? Epoch(Math.Abs(n) >= 1e10 ? n / 1000 : n) : node.Date();
    private static UsageSnapshot Result(string? plan, List<Metric> metrics, bool permitsEmpty = false)
    {
        if (metrics.Count == 0 && !permitsEmpty) throw Json.Invalid();
        return new(plan, metrics);
    }

    public static UsageSnapshot Claude(JsonNode body, JsonNode? profile = null, JsonNode? grants = null, DateTimeOffset? now = null)
    {
        Object(body);
        var rows = new List<Metric>();
        foreach (var (key, label, hours) in new[] { ("five_hour", "Session", 5d), ("seven_day", "Weekly", 168d), ("seven_day_sonnet", "Sonnet", 168d) })
            if (F(F(body, key), "utilization").Number() is double value)
                rows.Add(Percent(label, value, FlexibleDate(F(F(body, key), "resets_at")), hours));
        var fable = Items(F(body, "limits")).FirstOrDefault(entry => F(entry, "kind").Text() == "weekly_scoped" &&
            F(F(F(entry, "scope"), "model"), "display_name").Text() == "Fable");
        if (F(fable, "percent").Number() is double fableUsed) rows.Add(Percent("Fable", fableUsed, FlexibleDate(F(fable, "resets_at")), 168));
        var extra = F(body, "extra_usage");
        if (F(extra, "is_enabled").Bool() == true && F(extra, "used_credits").Number() is double cents)
        {
            var limit = F(extra, "monthly_limit").Number();
            var (code, places) = BillingCurrency(extra);
            var minorUnit = Math.Pow(10, places);
            if (limit > 0 || cents > 0) rows.Add(new("Extra Usage", Math.Max(0, cents / minorUnit), limit > 0 ? limit / minorUnit : null, "usd", CurrencyCode: code, CurrencyDecimalPlaces: places));
        }
        var reset = F(grants, "cedar_ember");
        if (reset is JsonObject)
        {
            var total = 0d;
            var valid = true;
            foreach (var grant in Items(F(reset, "grants")))
            {
                if (string.IsNullOrEmpty(F(grant, "id").Text()) || F(grant, "resets_left").Number() is not double left) { valid = false; break; }
                var expiry = FlexibleDate(F(grant, "ends_at"));
                if (F(reset, "eligible").Bool() == true && (expiry is null || expiry > (now ?? DateTimeOffset.UtcNow))) total += Math.Max(0, Math.Floor(left));
            }
            if (valid) rows.Add(new("Limit Resets", total, Unit: "resets"));
        }
        var organization = F(profile, "organization");
        var type = F(organization, "organization_type").Text();
        if (type?.StartsWith("claude_", StringComparison.Ordinal) == true) type = type[7..];
        var plan = Title(type);
        var tier = F(organization, "rate_limit_tier").Text();
        if (plan is not null && tier is not null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(tier, @"\d+x");
            if (match.Success) plan += " " + match.Value;
        }
        return Result(plan, rows);
    }

    // Each field falls back on its own: a bad code keeps a valid precision, and the reverse.
    private static (string Code, int Places) BillingCurrency(JsonNode? extra)
    {
        var code = F(extra, "currency").Text()?.Trim().ToUpperInvariant();
        var places = F(extra, "decimal_places").Number();
        return (code is { Length: 3 } && code.All(c => c is >= 'A' and <= 'Z') ? code : "USD",
            places is double p && p is >= 0 and <= 6 && p == Math.Floor(p) ? (int)p : 2);
    }

    public static UsageSnapshot Codex(JsonNode body, DateTimeOffset? now = null)
    {
        Object(body);
        var rows = new List<Metric>();
        CodexWindows(F(body, "rate_limit"), "Session", "Weekly", rows, now ?? DateTimeOffset.UtcNow);
        var spark = Items(F(body, "additional_rate_limits")).FirstOrDefault(entry =>
            new[] { F(entry, "limit_name").Text(), F(entry, "metered_feature").Text() }.Any(s => s?.Contains("spark", StringComparison.OrdinalIgnoreCase) == true));
        CodexWindows(F(spark, "rate_limit"), "Spark", "Spark Weekly", rows, now ?? DateTimeOffset.UtcNow);
        var credits = F(body, "credits");
        var balance = F(credits, "balance").Number() ?? (F(credits, "has_credits").Bool() == false ? 0 : null);
        if (balance is double b) rows.Add(new("Extra Usage", Math.Max(0, Math.Floor(b)), Unit: "credits"));
        if (F(F(body, "rate_limit_reset_credits"), "available_count").Number() is double count && count >= 0)
            rows.Add(new("Limit Resets", Math.Floor(count), Unit: "resets"));
        var rawPlan = F(body, "plan_type").Text();
        var plan = rawPlan?.ToLowerInvariant() switch
        {
            "prolite" => "Pro 100", "pro" => "Pro 200", "promax" => "Pro 500",
            "self_serve_business_prolite" => "Business Premium", _ => Title(rawPlan)
        };
        return Result(plan, rows);
    }

    private static void CodexWindows(JsonNode? rate, string sessionLabel, string weekLabel, List<Metric> rows, DateTimeOffset now)
    {
        var slots = new[] { F(rate, "primary_window"), F(rate, "secondary_window") };
        var periods = new[] { 18000d, 604800d };
        var labels = new[] { sessionLabel, weekLabel };
        for (var i = 0; i < 2; i++)
        {
            var exact = slots.FirstOrDefault(s => F(s, "limit_window_seconds").Number() == periods[i]);
            var fallback = F(slots[i], "limit_window_seconds").Number() is double seconds && periods.Contains(seconds) ? null : slots[i];
            var window = exact ?? fallback;
            if (F(window, "used_percent").Number() is not double value) continue;
            DateTimeOffset? reset = F(window, "reset_at").Date();
            if (reset is null && F(window, "reset_after_seconds").Number() is double after)
            {
                try { reset = now.AddSeconds(after); } catch (ArgumentOutOfRangeException) { }
            }
            rows.Add(Percent(labels[i], value, reset, exact is null ? null : periods[i] / 3600));
        }
    }

    public static UsageSnapshot Copilot(JsonNode body)
    {
        Object(body);
        var rows = new List<Metric>();
        var reset = F(body, "quota_reset_date").Date() ?? F(body, "limited_user_reset_date").Date();
        var snapshots = F(body, "quota_snapshots");
        foreach (var (key, label) in new[] { ("premium_interactions", "Credits"), ("chat", "Chat"), ("completions", "Completions") })
        {
            var bucket = F(snapshots, key);
            var limit = F(bucket, "entitlement").Number();
            var remaining = F(bucket, "remaining").Number();
            if (LooseBool(F(bucket, "unlimited")) == true || limit is -1 or 0 || remaining == -1) continue;
            var percent = F(bucket, "percent_remaining").Number();
            percent = percent.HasValue ? 100 - percent : limit > 0 && remaining.HasValue ? 100 - remaining / limit * 100 : null;
            if (percent is not double value) continue;
            rows.Add(Percent(label, value, reset));
            if (key == "premium_interactions" && LooseBool(F(bucket, "overage_permitted")) == true)
                rows.Add(new("Extra Usage", Math.Max(0, F(bucket, "overage_count").Number() ?? 0), Unit: "count"));
        }
        if (rows.Count == 0)
        {
            foreach (var (key, label) in new[] { ("chat", "Chat"), ("completions", "Completions") })
                if (F(F(body, "monthly_quotas"), key).Number() is double limit && limit > 0 &&
                    F(F(body, "limited_user_quotas"), key).Number() is double remaining)
                    rows.Add(Percent(label, Math.Max(0, limit - remaining) / limit * 100, reset));
        }
        var tokenBilling = LooseBool(F(body, "token_based_billing")) == true;
        if (rows.Count == 0 && tokenBilling && F(F(snapshots, "premium_interactions"), "credits_used").Number() is double used && used > 0)
            rows.Add(new("Credits", used, Unit: "credits"));
        return Result(Title(F(body, "copilot_plan").Text()), rows, tokenBilling);
    }

    public static UsageSnapshot Ollama(JsonNode body, JsonNode? account = null)
    {
        Object(body);
        if (F(body, "included") is not JsonObject included) throw Json.Invalid();
        var rows = new List<Metric>();
        foreach (var (key, label, hours) in new[] { ("session", "Session", 5d), ("weekly", "Weekly", 168d) })
            if (F(F(included, key), "remaining_percent").Number() is double remaining)
                rows.Add(Percent(label, 100 - remaining, F(F(included, key), "resets_at").Date(), hours));
        if (F(included, "allowance_usd").Number() is double allowance && allowance > 0 && F(included, "balance_usd").Number() is double balance)
            rows.Add(new("Monthly", Math.Min(allowance, Math.Max(0, allowance - balance)), allowance, "usd", F(F(included, "period"), "until").Date()));
        if (rows.Count == 0) throw Json.Invalid();
        if (F(F(body, "purchased"), "balance_usd").Number() is double purchased) rows.Add(new("Extra Usage", Math.Max(0, purchased), Unit: "usd"));
        return Result(Title(F(account, "Plan").Text() ?? F(account, "plan").Text()), rows);
    }

    public static UsageSnapshot OpenCode(JsonNode body, DateTimeOffset? now = null)
    {
        Object(body);
        var rows = new List<Metric>();
        foreach (var (key, label, hours) in new[] { ("rolling", "Session", 5d), ("weekly", "Weekly", 168d), ("monthly", "Monthly", 720d) })
        {
            var window = F(F(body, "usage"), key);
            if (F(window, "percent").Number() is not double percent || F(window, "resetsAt").Date() is not { } reset) throw Json.Invalid();
            DateTimeOffset? resetAt = reset;
            if (key == "rolling" && Clamp(percent) == 0 && Math.Abs((reset - (now ?? DateTimeOffset.UtcNow)).TotalSeconds - 18000) <= 2) resetAt = null;
            rows.Add(Percent(label, percent, resetAt, hours));
        }
        return new("Go", rows);
    }

    public static UsageSnapshot OpenRouter(JsonNode? credits, JsonNode? key)
    {
        var rows = new List<Metric>();
        var creditData = F(credits, "data");
        var keyData = F(key, "data");
        if (F(creditData, "total_usage").Number() is double used)
            rows.Add(new("Balance", Math.Max(0, Math.Max(0, F(creditData, "total_credits").Number() ?? 0) - Math.Max(0, used)), Unit: "usd"));
        foreach (var (field, label) in new[] { ("usage_daily", "Today"), ("usage_weekly", "This Week"), ("usage_monthly", "This Month") })
            if (F(keyData, field).Number() is double value) rows.Add(new(label, Math.Max(0, value), Unit: "usd"));
        var free = F(keyData, "is_free_tier").Bool();
        return Result(free.HasValue ? free.Value ? "Free" : "Pay-as-you-go" : null, rows);
    }

    public static UsageSnapshot Grok(JsonNode body, JsonNode? settings = null)
    {
        Object(body);
        var config = F(body, "config");
        var period = F(config, "currentPeriod");
        var start = F(period, "start").Date();
        var end = F(period, "end").Date();
        var type = F(period, "type").Text();
        if (string.IsNullOrWhiteSpace(type) || start is null || end is null || end <= start) throw Json.Invalid();
        var usedNode = F(config, "creditUsagePercent");
        var used = (config as JsonObject)?.ContainsKey("creditUsagePercent") != true ? 0 : usedNode.Number() ?? throw Json.Invalid();
        var capNode = F(config, "onDemandCap");
        if ((config as JsonObject)?.ContainsKey("onDemandCap") == true && capNode is not JsonObject) throw Json.Invalid();
        var capValue = F(capNode, "val");
        var cap = (capNode as JsonObject)?.ContainsKey("val") != true ? 0 : capValue.Number() ?? throw Json.Invalid();
        var rows = new List<Metric>();
        if (type == "USAGE_PERIOD_TYPE_WEEKLY") rows.Add(Percent("Weekly", used, end, (end.Value - start.Value).TotalHours));
        rows.Add(new("Extra Usage", cap, Unit: "count", Text: cap > 0 ? $"{cap:0.##} Cap" : "Disabled"));
        return new(F(settings, "subscription_tier_display").Text()?.Trim(), rows);
    }
}
