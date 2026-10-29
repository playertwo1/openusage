using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenUsage.Windows;

public static class LoginChecks
{
    public static void Run()
    {
        Check(ProviderLogin.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "RFC 7636 PKCE challenge");
        Check(ProviderLogin.ParseAuthorizationCode("https://localhost/callback?code=a%2Bb&state=s%20t", "s t") == "a+b", "encoded pasted callback");
        Check(ProviderLogin.ParseAuthorizationCode("code#state", "state") == "code", "pasted code and state");
        Reject(() => ProviderLogin.ParseAuthorizationCode("code", "state"), "bare code without state");
        Reject(() => ProviderLogin.ParseAuthorizationCode("code#wrong", "state"), "mismatched state");
        Reject(() => ProviderLogin.ParseAuthorizationCode("https://localhost/?code=a&state=s&state=s", "s"), "duplicate state");
        Reject(() => ProviderLogin.Token(JsonNode.Parse("{}")!), "missing access token");
        Reject(() => ProviderLogin.Token(JsonNode.Parse("[]")!), "non-object token response");
        Check(ProviderLogin.Jwt("invalid") is null && ProviderLogin.Jwt("x.@@@@.x") is null, "invalid JWT");
        Check(ProviderLogin.Jwt("x." + ProviderLogin.Base64Url(Encoding.UTF8.GetBytes("[]")) + ".x") is null, "non-object JWT claims");
        RefreshChecksAsync().GetAwaiter().GetResult();
        DevicePollingChecksAsync().GetAwaiter().GetResult();
        CallbackChecksAsync().GetAwaiter().GetResult();
    }

    private static async Task DevicePollingChecksAsync()
    {
        var polls = 0;
        using var api = new Api(new ReplyHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var status = HttpStatusCode.OK;
            var body = path switch
            {
                "/login/device/code" => "{\"device_code\":\"device\",\"user_code\":\"USER-CODE\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":30,\"interval\":1}",
                "/login/oauth/access_token" when ++polls == 1 => "{\"error\":\"authorization_pending\"}",
                "/login/oauth/access_token" => "{\"access_token\":\"authorized\"}",
                "/user" => "{\"login\":\"test-user\",\"id\":123}",
                _ => throw new Exception("Unexpected Login Request")
            };
            if (body.Contains("authorization_pending")) status = HttpStatusCode.BadRequest;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }));
        var prompted = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var login = await new ProviderLogin(api).SignInAsync(ProviderKind.Copilot, async (value, token) =>
        {
            prompted = value.Code == "USER-CODE" && value.Url == "https://github.com/login/device";
            await Task.Delay(Timeout.Infinite, token);
            return null;
        }, timeout.Token);
        Check(prompted && polls == 2 && login.Label == "test-user", "device polling survives authorization_pending");
        Check(login.Credential.Extras?["account_identity"] == "github:123", "device identity");
    }

    private static async Task RefreshChecksAsync()
    {
        foreach (var kind in new[] { ProviderKind.Claude, ProviderKind.Codex, ProviderKind.Cursor, ProviderKind.Antigravity, ProviderKind.Grok })
        {
            string? body = null;
            using var api = new Api(new ReplyHandler(async request =>
            {
                body = await request.Content!.ReadAsStringAsync();
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"new-access\",\"expires_in\":3600}") };
            }));
            var previous = new Credential("old-access", "old-refresh", Extras: new() { ["chatgpt_account_id"] = "account-id" });
            var next = await new ProviderLogin(api).RefreshAsync(kind, previous, CancellationToken.None);
            Check(next.AccessToken == "new-access", kind + " refresh access token");
            Check(next.RefreshToken == (kind == ProviderKind.Cursor ? "new-access" : "old-refresh"), kind + " refresh token preservation");
            Check(next.Extras?["chatgpt_account_id"] == "account-id", kind + " account preservation");
            Check(body is not null && body.Contains("old-refresh") && body.Contains("refresh_token"), kind + " refresh request");
        }
        using var rotatingApi = new Api(new ReplyHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"new\",\"refresh_token\":\"rotated\",\"expires_in\":3600}")
        })));
        var rotated = await new ProviderLogin(rotatingApi).RefreshAsync(ProviderKind.Claude, new("old", "previous"), CancellationToken.None);
        Check(rotated.RefreshToken == "rotated", "refresh token rotation");
        using var logoutApi = new Api(new ReplyHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"shouldLogout\":true,\"access_token\":\"ignored\"}")
        })));
        try
        {
            await new ProviderLogin(logoutApi).RefreshAsync(ProviderKind.Cursor, new("old", "previous"), CancellationToken.None);
            throw new Exception("Login Check Failed: Cursor Logout Must Be Rejected");
        }
        catch (ProviderException) { }
    }

    private static async Task CallbackChecksAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var callback = new LoopbackLogin("expected-state");
        var pending = callback.WaitForCodeAsync(timeout.Token);
        var port = new Uri(callback.RedirectUri).Port;
        var rejected = await RequestAsync(port, "/callback?code=stolen&state=wrong", timeout.Token);
        Check(rejected.StartsWith("HTTP/1.1 400") && !pending.IsCompleted, "callback rejects mismatched state");
        var wrongPath = await RequestAsync(port, "/other?code=value&state=expected-state", timeout.Token);
        Check(wrongPath.StartsWith("HTTP/1.1 404") && !pending.IsCompleted, "callback rejects other paths");
        var accepted = await RequestAsync(port, "/callback?code=accepted%2Bcode&state=expected-state", timeout.Token);
        Check(accepted.StartsWith("HTTP/1.1 200") && !accepted.Contains("Location:"), "callback fixed completion page");
        Check(await pending == "accepted+code", "callback accepts fragmented request");
    }
    private static async Task<string> RequestAsync(int port, string target, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, ct);
        var stream = client.GetStream();
        var bytes = Encoding.ASCII.GetBytes($"GET {target} HTTP/1.1\r\nHost: localhost:{port}\r\n\r\n");
        await stream.WriteAsync(bytes.AsMemory(0, 7), ct);
        await stream.WriteAsync(bytes.AsMemory(7), ct);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
    private static void Reject(Action action, string description)
    {
        try { action(); } catch (ProviderException) { return; }
        throw new Exception("Login Check Failed: " + description);
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("Login Check Failed: " + description);
    }
    private sealed class ReplyHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request);
    }
}
