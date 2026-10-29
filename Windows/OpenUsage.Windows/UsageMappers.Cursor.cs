using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public static partial class UsageMappers
{
    public static UsageSnapshot Cursor(JsonNode usage, JsonNode? planInfo = null, JsonNode? credits = null,
        JsonNode? stripe = null, JsonNode? summary = null, JsonNode? requests = null, JsonNode? grokBot = null)
    {
        Object(usage);
        if (F(usage, "enabled").Bool() == false) throw new ProviderException("No Active Cursor Subscription.");
        var planName = F(F(planInfo, "planInfo"), "planName").Text()?.Trim();
        var membership = F(summary, "membershipType").Text()?.Trim().ToLowerInvariant();
        if (membership is "enterprise" or "team" && (string.IsNullOrEmpty(planName) || planName.Equals("pro", StringComparison.OrdinalIgnoreCase) || planName.Equals("pro plan", StringComparison.OrdinalIgnoreCase))) planName = membership;
        var plan = F(usage, "planUsage");
        var limit = F(plan, "limit").Number();
        var totalPercent = F(plan, "totalPercentUsed").Number();
        var auto = F(plan, "autoPercentUsed").Number();
        var api = F(plan, "apiPercentUsed").Number();
        var spent = F(plan, "totalSpend").Number() ?? ((limit ?? 0) - (F(plan, "remaining").Number() ?? limit ?? 0));
        var hasPools = auto is >= 0 && api is >= 0 && (auto > 0 || api > 0 || spent == 0);
        var spendLimit = F(usage, "spendLimitUsage");
        var isTeam = planName?.Equals("team", StringComparison.OrdinalIgnoreCase) == true ||
            F(spendLimit, "limitType").Text()?.Equals("team", StringComparison.OrdinalIgnoreCase) == true ||
            F(spendLimit, "pooledLimit").Number() > 0;
        var fallback = plan is null || (limit is null && totalPercent is null && !hasPools);
        var rows = new List<Metric>();
        var reset = Milliseconds(F(usage, "billingCycleEnd"));
        if (fallback) CursorFallback(summary, requests, rows);
        else
        {
            if (isTeam && hasPools)
            {
                if (totalPercent is double p) rows.Add(Percent("Total Usage", p, reset));
            }
            else if (isTeam && limit is double teamLimit)
                rows.Add(new("Total Usage", Math.Max(0, spent / 100), Math.Max(0, teamLimit / 100), "usd", reset));
            else rows.Add(Percent("Total Usage", totalPercent ?? (limit > 0 ? spent / limit.Value * 100 : 0), reset));
            if (auto is double a) rows.Add(Percent("Cursor Models", a, reset));
            if (api is double b) rows.Add(Percent("Other Models", b, reset));
            if (spendLimit is JsonObject)
            {
                var spendCap = F(spendLimit, "individualLimit").Number() ?? F(spendLimit, "pooledLimit").Number() ?? 0;
                var remaining = F(spendLimit, "individualRemaining").Number() ?? F(spendLimit, "pooledRemaining").Number() ?? 0;
                var reported = new[] { F(spendLimit, "individualUsed").Number(), F(spendLimit, "pooledUsed").Number(), F(spendLimit, "totalSpend").Number() };
                var inferred = Math.Max(0, spendCap - remaining);
                var spend = reported.FirstOrDefault(x => x > 0) ?? (inferred > 0 ? inferred : reported.FirstOrDefault() ?? 0);
                if (spendCap > 0 || spend > 0) rows.Add(new("Extra Usage", Math.Max(0, spend / 100), spendCap > 0 ? spendCap / 100 : null, "usd"));
            }
            if (!rows.Any(r => r.Label == "Extra Usage")) CursorOnDemand(summary, rows, F(summary, "billingCycleEnd").Date());
        }
        if (grokBot is JsonObject && F(grokBot, "usesPooledEnterpriseAllowance").Bool() != true &&
            F(grokBot, "hasNonZeroIncludedLimit").Bool() != false && F(grokBot, "includedLimitZero").Bool() != true &&
            F(grokBot, "usagePercent").Number() is double botPercent && botPercent >= 0)
            rows.Add(Percent("Grok Bot", botPercent, F(grokBot, "nextResetTimestampUtc").Date()));
        var grantsEnabled = F(credits, "hasCreditGrants").Bool() == true;
        var grantTotal = grantsEnabled ? F(credits, "totalCents").Number() ?? 0 : 0;
        var grantUsed = grantsEnabled && grantTotal > 0 ? F(credits, "usedCents").Number() ?? 0 : 0;
        var stripeBalance = Math.Max(0, -(F(stripe, "customerBalance").Number() ?? 0));
        var totalCredit = Math.Max(0, grantTotal) + stripeBalance;
        if (totalCredit > 0) rows.Add(new("Credits", Math.Max(0, totalCredit - grantUsed) / 100, Unit: "usd"));
        var order = new[] { "Total Usage", "Cursor Models", "Other Models", "Extra Usage", "Grok Bot", "Requests", "Credits" };
        rows = rows.OrderBy(r => Array.IndexOf(order, r.Label)).ToList();
        return Result(Title(planName), rows);
    }

    private static void CursorFallback(JsonNode? summary, JsonNode? requests, List<Metric> rows)
    {
        var reset = F(summary, "billingCycleEnd").Date();
        if (reset is null && F(requests, "startOfMonth").Date() is { } start) reset = start.AddDays(30);
        var requestBucket = F(requests, "gpt-4");
        if (F(requestBucket, "maxRequestUsage").Number() is double requestLimit && requestLimit > 0)
        {
            var used = Math.Max(0, F(requestBucket, "numRequests").Number() ?? F(requestBucket, "numRequestsTotal").Number() ?? 0);
            rows.Add(new("Total Usage", used, requestLimit, "requests", reset));
            rows.Add(new("Requests", used, requestLimit, "requests", reset));
        }
        else
        {
            var individual = F(summary, "individualUsage");
            var pooled = F(F(summary, "teamUsage"), "pooled");
            if (F(summary, "limitType").Text()?.ToLowerInvariant() == "team" && DollarMeter(pooled) is { } pool)
                rows.Add(new("Total Usage", pool.Used / 100, pool.Limit / 100, "usd", reset));
            else if (F(F(individual, "plan"), "totalPercentUsed").Number() is double percent)
                rows.Add(Percent("Total Usage", percent, reset));
            else if ((DollarMeter(F(individual, "overall")) ?? DollarMeter(pooled)) is { } overall)
                rows.Add(new("Total Usage", overall.Used / 100, overall.Limit / 100, "usd", reset));
        }
        var plan = F(F(summary, "individualUsage"), "plan");
        if (F(plan, "autoPercentUsed").Number() is double auto) rows.Add(Percent("Cursor Models", auto, reset));
        if (F(plan, "apiPercentUsed").Number() is double api) rows.Add(Percent("Other Models", api, reset));
        CursorOnDemand(summary, rows, reset);
    }

    private static (double Used, double Limit)? DollarMeter(JsonNode? bucket)
    {
        if (bucket is not JsonObject || F(bucket, "enabled").Bool() == false || F(bucket, "limit").Number() is not double limit || limit <= 0) return null;
        var reported = F(bucket, "used").Number();
        var inferred = Math.Max(0, limit - (F(bucket, "remaining").Number() ?? limit));
        return (Math.Max(0, reported > 0 ? reported.Value : inferred), limit);
    }

    private static void CursorOnDemand(JsonNode? summary, List<Metric> rows, DateTimeOffset? reset)
    {
        foreach (var bucket in new[] { F(F(summary, "individualUsage"), "onDemand"), F(F(summary, "teamUsage"), "onDemand") })
        {
            if (bucket is not JsonObject || F(bucket, "enabled").Bool() == false) continue;
            if (DollarMeter(bucket) is { } meter)
            {
                rows.Add(new("Extra Usage", meter.Used / 100, meter.Limit / 100, "usd", reset));
                return;
            }
            if (F(bucket, "used").Number() is double used && used > 0)
            {
                rows.Add(new("Extra Usage", used / 100, Unit: "usd"));
                return;
            }
        }
    }
}
