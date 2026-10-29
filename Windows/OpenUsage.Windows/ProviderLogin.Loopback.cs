using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenUsage.Windows;

internal sealed class LoopbackLogin : IDisposable
{
    private readonly TcpListener listener;
    private readonly string state;
    private readonly string path;
    public string RedirectUri { get; }
    public LoopbackLogin(string state, int port = 0, string path = "/callback")
    {
        this.state = state; this.path = path;
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start(4);
        RedirectUri = $"http://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}{path}";
    }
    public async Task<string> WaitForCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            using var connection = await listener.AcceptTcpClientAsync(ct);
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var stream = connection.GetStream();
            var buffer = new byte[16384];
            var length = 0;
            try
            {
                while (length < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(length), requestTimeout.Token);
                    if (read == 0) break;
                    length += read;
                    if (Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { continue; }
            catch (IOException) { continue; }
            var request = Encoding.ASCII.GetString(buffer, 0, length);
            var requestLine = request.Split("\r\n", 2)[0].Split(' ');
            if (!request.Contains("\r\n\r\n", StringComparison.Ordinal) || requestLine.Length != 3 || requestLine[0] != "GET")
            {
                await ReplyAsync(stream, 400, "Invalid Sign-In Request.", ct); continue;
            }
            if (!requestLine[1].StartsWith('/') || requestLine[1].StartsWith("//") ||
                !Uri.TryCreate("http://localhost" + requestLine[1], UriKind.Absolute, out var uri) || uri.AbsolutePath != path)
            {
                await ReplyAsync(stream, 404, "Callback Not Found.", ct); continue;
            }
            try
            {
                var values = ProviderLogin.Query(uri.Query);
                if (values.GetValueOrDefault("state") != state)
                {
                    await ReplyAsync(stream, 400, "Sign-In State Did Not Match.", ct); continue;
                }
                if (values.ContainsKey("error"))
                {
                    await ReplyAsync(stream, 400, "Authorization Was Denied.", ct);
                    throw new ProviderException("Authorization Was Denied In The Browser.");
                }
                var code = ProviderLogin.ParseAuthorizationCode(uri.AbsoluteUri, state);
                await ReplyAsync(stream, 200, "Sign-In Complete. You May Close This Page.", ct);
                return code;
            }
            catch (ProviderException error) when (error.Message != "Authorization Was Denied In The Browser.")
            {
                await ReplyAsync(stream, 400, "Invalid Sign-In Response.", ct);
            }
        }
    }
    private static async Task ReplyAsync(NetworkStream stream, int status, string message, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes("<!doctype html><title>OpenUsage</title><p>" + message + "</p>");
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        try { await stream.WriteAsync(header, ct); await stream.WriteAsync(body, ct); }
        catch (IOException) { /* A closed browser tab does not discard an accepted code. */ }
    }
    public void Dispose() => listener.Stop();
}
