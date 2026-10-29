using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

[assembly: InternalsVisibleTo("OpenUsage.Windows.Tests")]

namespace OpenUsage.Windows;

public sealed partial class ProviderLogin(Api api)
{
    // Cursor's JWT is also its refresh token; renew before a week-long sleep can expire it.
    internal static bool NeedsRefresh(ProviderKind kind, Credential credential, DateTimeOffset now) =>
        credential.RefreshToken is not null && credential.ExpiresAt <= now.Add(kind == ProviderKind.Cursor ? TimeSpan.FromDays(7) : TimeSpan.FromMinutes(5));

    private const string ClaudeClient = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    private const string CodexClient = "app_EMoamEEZ73f0CkXaXp7hrann";
    private const string GoogleClient = "1071006060591-tmhssin2h21lcre235vtolojh4g403ep.apps.googleusercontent.com";
    // Google's installed-app client secret is public and required by its token grant.
    private const string GoogleSecret = "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";
    private const string GrokClient = "b1a00492-073a-47ea-816f-4c329264a828";
    private const string CursorClient = "KbZUR41cY7W6zRSdpSUJ7I7mLYBKOCmB";
    private const string ClaudeTokenUrl = "https://platform.claude.com/v1/oauth/token";
    private const string CodexTokenUrl = "https://auth.openai.com/oauth/token";
    private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
    private const string GrokTokenUrl = "https://auth.x.ai/oauth2/token";

    public async Task<LoginResult> SignInAsync(ProviderKind kind,
        Func<LoginPrompt, CancellationToken, Task<string?>> prompt, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            return kind switch
            {
                ProviderKind.Claude or ProviderKind.Codex or ProviderKind.Antigravity => await BrowserSignInAsync(kind, prompt, timeout.Token),
                ProviderKind.Cursor => await CursorSignInAsync(prompt, timeout.Token),
                ProviderKind.Copilot or ProviderKind.Grok => await DeviceSignInAsync(kind, prompt, timeout.Token),
                _ => throw new ProviderException("This Provider Requires An API Key.")
            };
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new ProviderException("Sign-In Timed Out. Please Try Again.");
        }
    }

    public async Task<Credential> RefreshAsync(ProviderKind kind, Credential credential, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credential.RefreshToken)) return credential;
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = credential.RefreshToken
        };
        string url;
        object body;
        switch (kind)
        {
            case ProviderKind.Claude:
                url = ClaudeTokenUrl; fields["client_id"] = ClaudeClient; fields["scope"] = "user:profile";
                body = new { grant_type = "refresh_token", refresh_token = credential.RefreshToken, client_id = ClaudeClient, scope = "user:profile" };
                break;
            case ProviderKind.Codex:
                url = CodexTokenUrl; fields["client_id"] = CodexClient; body = fields; break;
            case ProviderKind.Antigravity:
                url = GoogleTokenUrl; fields["client_id"] = GoogleClient; fields["client_secret"] = GoogleSecret; body = fields; break;
            case ProviderKind.Grok:
                url = GrokTokenUrl; fields["client_id"] = GrokClient; body = fields; break;
            case ProviderKind.Cursor:
                url = "https://api2.cursor.sh/oauth/token";
                body = new { grant_type = "refresh_token", client_id = CursorClient, refresh_token = credential.RefreshToken }; break;
            default: return credential;
        }
        var data = await api.SendAsync(HttpMethod.Post, url, body: body, ct: ct);
        if (data["shouldLogout"].Bool() == true) throw new ProviderException("Your Sign-In Has Expired. Sign In Again.");
        var token = Token(data, credential);
        if (kind == ProviderKind.Cursor)
            token = token with { RefreshToken = data["refresh_token"].Text() ?? token.AccessToken, ExpiresAt = Jwt(token.AccessToken)?["exp"].Date() };
        if (kind == ProviderKind.Antigravity && token.ExpiresAt is null)
            token = token with { ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        return token;
    }

    private async Task<LoginResult> BrowserSignInAsync(ProviderKind kind,
        Func<LoginPrompt, CancellationToken, Task<string?>> prompt, CancellationToken ct)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var state = kind == ProviderKind.Codex ? Guid.NewGuid().ToString() : verifier;
        LoopbackLogin callback;
        try
        {
            callback = new LoopbackLogin(state, kind == ProviderKind.Codex ? 1455 : kind == ProviderKind.Antigravity ? 51121 : 0,
                kind == ProviderKind.Codex ? "/auth/callback" : kind == ProviderKind.Antigravity ? "/oauth-callback" : "/callback");
        }
        catch (SocketException) when (kind == ProviderKind.Codex) { return await CodexDeviceSignInAsync(prompt, ct); }
        catch (SocketException) { throw new ProviderException("The Local Sign-In Port Is In Use. Close Other Sign-In Windows And Try Again."); }
        using (callback)
        {
            var client = kind == ProviderKind.Claude ? ClaudeClient : kind == ProviderKind.Codex ? CodexClient : GoogleClient;
            var url = kind == ProviderKind.Claude ? "https://claude.ai/oauth/authorize" : kind == ProviderKind.Codex ? "https://auth.openai.com/oauth/authorize" : "https://accounts.google.com/o/oauth2/v2/auth";
            var scope = kind == ProviderKind.Claude ? "user:profile" : kind == ProviderKind.Codex ? "openid profile email offline_access" : "https://www.googleapis.com/auth/cloud-platform https://www.googleapis.com/auth/userinfo.email";
            var query = new Dictionary<string, string>
            {
                ["client_id"] = client, ["response_type"] = "code", ["redirect_uri"] = callback.RedirectUri,
                ["scope"] = scope, ["code_challenge"] = Challenge(verifier), ["code_challenge_method"] = "S256", ["state"] = state
            };
            if (kind == ProviderKind.Claude) query["code"] = "true";
            if (kind == ProviderKind.Codex)
            {
                query["id_token_add_organizations"] = "true"; query["codex_cli_simplified_flow"] = "true"; query["originator"] = "pi";
            }
            if (kind == ProviderKind.Antigravity) { query["access_type"] = "offline"; query["prompt"] = "consent"; }
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var input = prompt(new LoginPrompt("Complete Sign-In In Your Browser. You Can Also Paste The Full Callback URL Or Code#State.", QueryUrl(url, query), AcceptsPastedCode: true), waiting.Token);
            var received = callback.WaitForCodeAsync(waiting.Token);
            string code;
            try
            {
                var winner = await Task.WhenAny((Task)input, received);
                code = winner == received ? await received : ParseAuthorizationCode(await input ?? throw new OperationCanceledException(ct), state);
            }
            finally { waiting.Cancel(); }
            if (kind == ProviderKind.Codex) return await ExchangeCodexAsync(code, verifier, callback.RedirectUri, ct);
            var fields = new Dictionary<string, string>
            {
                ["client_id"] = client, ["grant_type"] = "authorization_code", ["code"] = code,
                ["redirect_uri"] = callback.RedirectUri, ["code_verifier"] = verifier
            };
            JsonNode data;
            if (kind == ProviderKind.Claude)
                data = await api.SendAsync(HttpMethod.Post, ClaudeTokenUrl, body: new { grant_type = "authorization_code", client_id = client, code, state, redirect_uri = callback.RedirectUri, code_verifier = verifier }, ct: ct);
            else
            {
                fields["client_secret"] = GoogleSecret;
                data = await api.SendAsync(HttpMethod.Post, GoogleTokenUrl, body: fields, ct: ct);
            }
            var credential = Token(data);
            if (kind == ProviderKind.Claude)
            {
                // Identity is optional in the Swift login too; usage refresh reports provider failures.
                try
                {
                    var profile = await api.GetAsync("https://api.anthropic.com/api/oauth/profile", credential.AccessToken, ct: ct);
                    var identity = profile["account"]?["uuid"].Text();
                    if (identity is not null && profile["organization"]?["uuid"].Text() is string organization) identity += "/" + organization;
                    return new(profile["account"]?["email"].Text() ?? profile["email"].Text() ?? "Claude Account", credential with { Extras = Identity(identity) });
                }
                catch (ProviderException) { return new("Claude Account", credential); }
            }
            var user = await api.GetAsync("https://www.googleapis.com/oauth2/v1/userinfo?alt=json", credential.AccessToken, ct: ct);
            return new(user["email"].Text() ?? "Google Account", credential with { Extras = Identity(user["id"].Text() ?? user["email"].Text()) });
        }
    }

    private async Task<LoginResult> ExchangeCodexAsync(string code, string verifier, string redirectUri, CancellationToken ct)
    {
        var data = await api.SendAsync(HttpMethod.Post, CodexTokenUrl, body: new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = CodexClient, ["code"] = code,
            ["code_verifier"] = verifier, ["redirect_uri"] = redirectUri
        }, ct: ct);
        var credential = Token(data);
        var accountId = Jwt(credential.AccessToken)?["https://api.openai.com/auth"]?["chatgpt_account_id"].Text();
        var email = Jwt(data["id_token"].Text())?["email"].Text();
        var extras = new Dictionary<string, string>();
        if (accountId is not null) extras["chatgpt_account_id"] = accountId;
        if (accountId is not null && email is not null) extras["account_identity"] = accountId + "/" + email;
        return new(email ?? "ChatGPT Account", credential with { Extras = extras });
    }

    internal static Credential Token(JsonNode data, Credential? previous = null)
    {
        if (data is not JsonObject) throw Json.Invalid();
        var access = data["access_token"].Text();
        if (string.IsNullOrWhiteSpace(access)) throw Json.Invalid();
        var seconds = data["expires_in"].Number();
        DateTimeOffset? expiry = Jwt(access)?["exp"].Date();
        if (seconds is > 0 and < 315360000) expiry = DateTimeOffset.UtcNow.AddSeconds(seconds.Value);
        return new(access, data["refresh_token"].Text() ?? previous?.RefreshToken, expiry, previous?.Extras);
    }
    internal static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    internal static JsonNode? Jwt(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[1].Length > 65536) return null;
        try
        {
            var text = parts[1].Replace('-', '+').Replace('_', '/');
            return JsonNode.Parse(Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '='))) as JsonObject;
        }
        catch (Exception error) when (error is FormatException or System.Text.Json.JsonException) { return null; }
    }
    private static Dictionary<string, string> Identity(string? identity) => identity is null ? [] : new() { ["account_identity"] = identity };
    internal static string QueryUrl(string url, Dictionary<string, string> values) => url + "?" + string.Join("&", values.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
    internal static Dictionary<string, string> Query(string query)
    {
        var values = new Dictionary<string, string>();
        foreach (var field in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = field.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            if (!values.TryAdd(key, parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : ""))
                throw new ProviderException("The Sign-In Response Contains Duplicate Fields.");
        }
        return values;
    }
    internal static string ParseAuthorizationCode(string value, string state)
    {
        var text = value.Trim();
        string? code; string? receivedState;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var query = Query(uri.Query); code = query.GetValueOrDefault("code"); receivedState = query.GetValueOrDefault("state");
        }
        else
        {
            var parts = text.Split('#', 2); code = parts[0]; receivedState = parts.Length > 1 ? parts[1] : null;
        }
        if (string.IsNullOrWhiteSpace(code)) throw new ProviderException("The Authorization Code Is Missing.");
        if (receivedState != state) throw new ProviderException("The Sign-In Response Did Not Match This Request.");
        return code;
    }
}
