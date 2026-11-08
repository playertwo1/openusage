using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public static partial class UsageMappers
{
    private static readonly (string Id, string Label, double Hours)[] AntigravityBuckets =
    [
        ("gemini-5h", "Gemini 5h", 5), ("gemini-weekly", "Gemini", 168),
        ("3p-5h", "Claude 5h", 5), ("3p-weekly", "Claude Weekly", 168)
    ];

    private static IEnumerable<JsonNode> Groups(JsonNode body) => Items(F(F(body, "response"), "groups") ?? F(body, "groups"));

    public static bool CompleteAntigravity(JsonNode body)
    {
        var usable = Groups(body).Where(g => F(g, "displayName").Text() != "Gemini 4 Argon")
            .SelectMany(g => Items(F(g, "buckets"))).Where(b => F(b, "remainingFraction").Number().HasValue)
            .Select(b => F(b, "bucketId").Text()).ToHashSet();
        return AntigravityBuckets.All(spec => usable.Contains(spec.Id));
    }

    // Google's primary host can report a window that has not started as "resets one window from now" with full quota.
    // The tolerance covers the gap between sending the request and the server's clock.
    private const double UnstartedToleranceSeconds = 60;

    private static bool Unstarted(JsonNode bucket, double hours, DateTimeOffset requested) =>
        F(bucket, "resetTime").Date() is DateTimeOffset reset && Math.Abs((reset - requested).TotalSeconds - hours * 3600) <= UnstartedToleranceSeconds;

    // True when a summary holds an unstarted window, so the daily host should be read for its real value.
    public static bool AntigravityNeedsFallback(JsonNode body, DateTimeOffset requested) =>
        Groups(body).Where(g => F(g, "displayName").Text() != "Gemini 4 Argon").SelectMany(g => Items(F(g, "buckets")))
            .Any(b => AntigravityBuckets.Any(spec => spec.Id == F(b, "bucketId").Text() && Unstarted(b, spec.Hours, requested)));

    public static UsageSnapshot Antigravity(IEnumerable<JsonNode> summaries, JsonNode? assist = null, DateTimeOffset? requested = null)
    {
        var now = requested ?? DateTimeOffset.UtcNow;
        var groups = summaries.SelectMany(Groups).ToArray();
        var pool = groups.Where(g => F(g, "displayName").Text() != "Gemini 4 Argon").SelectMany(g => Items(F(g, "buckets")))
            .Where(b => F(b, "remainingFraction").Number() is not null).ToArray();
        var pooled = new Dictionary<string, JsonNode>();
        foreach (var bucket in pool)
        {
            var id = F(bucket, "bucketId").Text();
            if (id is null || !AntigravityBuckets.Any(spec => spec.Id == id) || pooled.ContainsKey(id)) continue;
            pooled[id] = bucket;
        }
        // An unstarted window is replaced by the same bucket from another host when that host reports a started one.
        foreach (var (id, _, hours) in AntigravityBuckets)
        {
            if (!pooled.TryGetValue(id, out var chosen) || !Unstarted(chosen, hours, now)) continue;
            if (pool.FirstOrDefault(b => F(b, "bucketId").Text() == id && !Unstarted(b, hours, now)) is { } started) pooled[id] = started;
        }
        var rows = new List<Metric>();
        foreach (var (id, label, hours) in AntigravityBuckets)
        {
            if (!pooled.TryGetValue(id, out var bucket)) continue;
            rows.Add(AntigravityPercent(label, bucket, hours));
        }
        foreach (var groupList in summaries.Select(s => Groups(s).ToArray()))
        {
            var weekly = groupList.Where(g => F(g, "displayName").Text() == "Gemini 4 Argon")
                .SelectMany(g => Items(F(g, "buckets"))).Where(b => F(b, "window").Text() == "weekly").ToArray();
            if (weekly.Length != 1) continue;
            var bucket = weekly[0];
            var id = F(bucket, "bucketId").Text();
            var disabled = F(bucket, "disabled");
            if (string.IsNullOrEmpty(id) || AntigravityBuckets.Any(spec => spec.Id == id) ||
                (disabled is not null && disabled.Bool() is null) || disabled.Bool() == true || F(bucket, "remainingFraction").Number() is null) continue;
            var index = rows.FindIndex(r => r.Label is "Claude 5h" or "Claude Weekly");
            rows.Insert(index < 0 ? rows.Count : index, AntigravityPercent("Argon", bucket, WeeklyHours));
            break;
        }
        var paid = F(assist, "paidTier");
        var idValue = F(paid, "id").Text();
        var plan = idValue switch
        {
            "g1-pro-tier" => "Google AI Pro", "g1-ultra-tier" => "Google AI Ultra", "g1-ultra-lite-tier" => "Google AI Ultra Lite",
            _ => F(paid, "name").Text() ?? F(F(assist, "currentTier"), "name").Text()
        };
        if (plan is not null)
            foreach (var tier in new[] { "Ultra Lite", "Ultra", "Pro" })
                if (plan.Contains(tier, StringComparison.OrdinalIgnoreCase)) { plan = "Google AI " + tier; break; }
        return Result(plan, rows);
    }

    private const double WeeklyHours = 168;

    private static Metric AntigravityPercent(string label, JsonNode bucket, double hours)
    {
        var remaining = F(bucket, "remainingFraction").Number() ?? throw Json.Invalid();
        return Percent(label, Math.Round((1 - Math.Clamp(remaining, 0, 1)) * 100, MidpointRounding.AwayFromZero), F(bucket, "resetTime").Date(), hours);
    }
}
