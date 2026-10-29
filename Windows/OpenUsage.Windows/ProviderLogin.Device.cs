using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public sealed partial class ProviderLogin
{
    private async Task<LoginResult> DeviceSignInAsync(ProviderKind kind,
        Func<LoginPrompt, CancellationToken, Task<string?>> prompt, CancellationToken ct)
    {
        var copilot = kind == ProviderKind.Copilot;
        var client = copilot ? "Iv1.b507a08c87ecfe98" : GrokClient;
        var tokenUrl = copilot ? "https://github.com/login/oauth/access_token" : GrokTokenUrl;
        var start = await api.SendAsync(HttpMethod.Post,
            copilot ? "https://github.com/login/device/code" : "https://auth.x.ai/oauth2/device/code",
            body: new Dictionary<string, string> { ["client_id"] = client, ["scope"] = copilot ? "read:user" : "openid profile email offline_access grok-cli:access api:access" }, ct: ct);
        var code = Required(start, "device_code");
        var userCode = Required(start, "user_code");
        var verification = Required(start, "verification_uri");
        if (!Uri.TryCreate(verification, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw Json.Invalid();
        var expiry = start["expires_in"].Number();
        if (expiry is not > 0 or > 86400) throw Json.Invalid();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(expiry.Value));
        var interval = PollInterval(start["interval"].Number(), 5);
        var data = await PromptDuringAsync(new LoginPrompt("Enter This Code In Your Browser To Sign In.", verification, userCode), prompt, async token =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), token);
                var response = await api.SendAsync(HttpMethod.Post, tokenUrl, body: new Dictionary<string, string>
                {
                    ["client_id"] = client, ["device_code"] = code, ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
                }, ct: token, allowOAuthError: true);
                if (!string.IsNullOrEmpty(response["access_token"].Text())) return response;
                switch (response["error"].Text())
                {
                    case "authorization_pending": break;
                    case "slow_down": interval += 5; break;
                    case "expired_token": throw new ProviderException("Device Sign-In Expired. Please Try Again.");
                    case "access_denied": throw new ProviderException("Device Sign-In Was Denied.");
                    default: throw new ProviderException("Device Sign-In Failed. Please Try Again.");
                }
            }
        }, timeout.Token);
        var credential = Token(data);
        if (copilot)
        {
            var user = await api.GetAsync("https://api.github.com/user", credential.AccessToken,
                new() { ["X-GitHub-Api-Version"] = "2022-11-28" }, ct);
            var login = Required(user, "login");
            var identity = user["id"].Number()?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? login;
            return new(login, credential with { Extras = Identity("github:" + identity) });
        }
        var claims = Jwt(credential.AccessToken);
        return new(claims?["email"].Text() ?? "Grok Account", credential with { Extras = Identity(claims?["sub"].Text() ?? claims?["email"].Text()) });
    }

    private async Task<LoginResult> CursorSignInAsync(Func<LoginPrompt, CancellationToken, Task<string?>> prompt, CancellationToken ct)
    {
        var verifier = Base64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var uuid = Guid.NewGuid().ToString();
        var url = QueryUrl("https://cursor.com/loginDeepControl", new()
        {
            ["challenge"] = Challenge(verifier), ["uuid"] = uuid, ["mode"] = "login", ["redirectTarget"] = "cli"
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var data = await PromptDuringAsync(new LoginPrompt("Complete Cursor Sign-In In Your Browser.", url), prompt, async token =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                JsonNode response;
                try
                {
                    response = await api.GetAsync(QueryUrl("https://api2.cursor.sh/auth/poll", new() { ["uuid"] = uuid, ["verifier"] = verifier }), ct: token);
                }
                catch (ProviderException error) when (error.StatusCode is 404 or 202) { continue; }
                if (response is JsonObject { Count: 0 }) continue;
                if (!string.IsNullOrWhiteSpace(response["accessToken"].Text()) && !string.IsNullOrWhiteSpace(response["refreshToken"].Text())) return response;
                // Cursor's pending poll is HTTP 202; Api preserves its status for an empty body.
                throw new ProviderException("Cursor Sign-In Failed. Please Try Again.");
            }
        }, timeout.Token);
        var access = Required(data, "accessToken");
        var subject = Jwt(access)?["sub"].Text()?.Trim();
        var userId = subject?.Split('|').ElementAtOrDefault(subject.Contains('|') ? 1 : 0);
        return new("Cursor Account", new(access, Required(data, "refreshToken"), Jwt(access)?["exp"].Date(), Identity(string.IsNullOrWhiteSpace(userId) ? null : "cursor:" + userId)));
    }

    private async Task<LoginResult> CodexDeviceSignInAsync(Func<LoginPrompt, CancellationToken, Task<string?>> prompt, CancellationToken ct)
    {
        var start = await api.SendAsync(HttpMethod.Post, "https://auth.openai.com/api/accounts/deviceauth/usercode", body: new { client_id = CodexClient }, ct: ct);
        var deviceId = Required(start, "device_auth_id");
        var userCode = Required(start, "user_code");
        var interval = PollInterval(start["interval"].Number(), 5);
        var response = await PromptDuringAsync(new LoginPrompt("Enter This Code In Your Browser To Sign In.", "https://auth.openai.com/codex/device", userCode), prompt, async token =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), token);
                JsonNode data;
                try
                {
                    data = await api.SendAsync(HttpMethod.Post, "https://auth.openai.com/api/accounts/deviceauth/token",
                        body: new { device_auth_id = deviceId, user_code = userCode }, ct: token, allowOAuthError: true);
                }
                catch (ProviderException error) when (error.StatusCode is 403 or 404) { continue; }
                if (!string.IsNullOrEmpty(data["authorization_code"].Text())) return data;
                switch (data["error"].Text())
                {
                    case "deviceauth_authorization_pending": break;
                    case "slow_down": interval += 5; break;
                    case "expired_token": throw new ProviderException("Device Sign-In Expired. Please Try Again.");
                    default: throw new ProviderException("Device Sign-In Failed. Please Try Again.");
                }
            }
        }, ct);
        return await ExchangeCodexAsync(Required(response, "authorization_code"), Required(response, "code_verifier"), "https://auth.openai.com/deviceauth/callback", ct);
    }

    private static async Task<T> PromptDuringAsync<T>(LoginPrompt value, Func<LoginPrompt, CancellationToken, Task<string?>> prompt,
        Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var shown = prompt(value, waiting.Token);
        var operation = action(waiting.Token);
        try
        {
            if (await Task.WhenAny(shown, operation) == shown)
            {
                await shown;
                throw new OperationCanceledException(ct);
            }
            return await operation;
        }
        finally { waiting.Cancel(); }
    }
    private static string Required(JsonNode node, string key) => !string.IsNullOrWhiteSpace(node[key].Text()) ? node[key].Text()! : throw Json.Invalid();
    private static double PollInterval(double? interval, double fallback) => interval is > 0 and <= 86400 ? Math.Max(1, interval.Value) : fallback;
}
