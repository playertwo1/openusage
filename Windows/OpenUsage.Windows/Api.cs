using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public sealed class Api : IDisposable
{
    private readonly HttpClient client;
    public Api(HttpMessageHandler? handler = null)
    {
        client = handler is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenUsage");
    }
    public async Task<JsonNode> SendAsync(HttpMethod method, string url, string? token = null,
        object? body = null, Dictionary<string, string>? headers = null, CancellationToken ct = default, bool allowOAuthError = false)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (headers is not null)
            foreach (var (key, value) in headers)
            {
                request.Headers.Remove(key);
                request.Headers.TryAddWithoutValidation(key, value);
            }
        if (body is Dictionary<string, string> form) request.Content = new FormUrlEncodedContent(form);
        else if (body is not null) request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && !(allowOAuthError && response.StatusCode == HttpStatusCode.BadRequest))
            throw new ProviderException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your Sign-In Has Expired. Remove This Account And Sign In Again.",
                HttpStatusCode.Forbidden => "The Provider Denied Access. Check The Account's Plan And Permissions.",
                HttpStatusCode.TooManyRequests => "The Provider Is Rate Limiting Requests. Try Again Later.",
                _ => $"The Provider Returned HTTP {(int)response.StatusCode}. Try Again Later."
            }, (int)response.StatusCode);
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (allowOAuthError && response.StatusCode == HttpStatusCode.Accepted && string.IsNullOrWhiteSpace(content)) return new JsonObject();
            return JsonNode.Parse(content)
                ?? throw new ProviderException("The Provider Returned An Empty Response.", (int)response.StatusCode);
        }
        catch (System.Text.Json.JsonException) { throw new ProviderException("The Provider Returned An Invalid Response.", (int)response.StatusCode); }
    }
    public Task<JsonNode> GetAsync(string url, string? token = null, Dictionary<string, string>? headers = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, url, token, headers: headers, ct: ct);
    public void Dispose() => client.Dispose();
}

public static class Json
{
    public static string? Text(this JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
    public static double? Number(this JsonNode? node)
    {
        if (node is not JsonValue v) return null;
        return double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    }
    public static bool? Bool(this JsonNode? node) => node is JsonValue v && v.TryGetValue<bool>(out var value) ? value : null;
    public static DateTimeOffset? Date(this JsonNode? node)
    {
        if (node.Number() is double n)
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(checked((long)n)); } catch (ArgumentOutOfRangeException) { return null; } catch (OverflowException) { return null; }
        }
        return DateTimeOffset.TryParse(node.Text(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
    }
    public static ProviderException Invalid() => new("The Provider Returned Incomplete Or Unrecognized Usage Data.");
}
