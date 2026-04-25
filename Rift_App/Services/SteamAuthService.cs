using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace Rift_App.Services
{
    public static class SteamAuthService
    {
        private const string SteamOpenIdUrl = "https://steamcommunity.com/openid/login";
        private const string CallbackUrl = "http://localhost:7777/rift/callback/";
        private const string ListenerUrl = "http://localhost:7777/rift/";

        private static HttpListener? _listener;
        private static CancellationTokenSource? _cts;

        public static async Task<string?> LoginAsync()
        {
            StopListener();
            _cts = new CancellationTokenSource();

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(ListenerUrl);
                _listener.Start();

                Process.Start(new ProcessStartInfo
                {
                    FileName = BuildSteamLoginUrl(),
                    UseShellExecute = true
                });

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

                return await WaitForCallbackAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) { return null; }
            catch { return null; }
            finally { StopListener(); }
        }

        public static void Cancel()
        {
            _cts?.Cancel();
            StopListener();
        }

        private static async Task<string?> WaitForCallbackAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var contextTask = _listener!.GetContextAsync();
                    var completedTask = await Task.WhenAny(contextTask, Task.Delay(500, token));

                    if (token.IsCancellationRequested) return null;
                    if (completedTask != contextTask) continue;

                    var context = await contextTask;
                    var url = context.Request.Url?.ToString() ?? string.Empty;
                    await SendBrowserResponse(context.Response);
                    return ExtractSteamId(url);
                }
                catch (OperationCanceledException) { return null; }
                catch (HttpListenerException) { return null; }
                catch { return null; }
            }
            return null;
        }

        private static async Task SendBrowserResponse(HttpListenerResponse response)
        {
            try
            {
                var html = """
                    <!DOCTYPE html><html>
                    <head><title>Rift — Login Successful</title>
                    <style>body{background:#1b2838;color:white;font-family:Arial,sans-serif;
                    display:flex;justify-content:center;align-items:center;height:100vh;margin:0;}
                    h1{color:#66c0f4;}p{color:#8b929a;}</style></head>
                    <body><div style="text-align:center">
                    <h1>Login Successful</h1>
                    <p>You can close this window and return to Rift.</p>
                    </div><script>setTimeout(()=>window.close(),2000);</script></body></html>
                    """;
                var buffer = System.Text.Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
                response.OutputStream.Close();
            }
            catch { }
        }

        private static string? ExtractSteamId(string callbackUrl)
        {
            try
            {
                var match = Regex.Match(callbackUrl, @"openid\.claimed_id=.*?\/(\d{17})");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch { return null; }
        }

        private static string BuildSteamLoginUrl() =>
            $"{SteamOpenIdUrl}" +
            $"?openid.ns={Uri.EscapeDataString("http://specs.openid.net/auth/2.0")}" +
            $"&openid.mode=checkid_setup" +
            $"&openid.return_to={Uri.EscapeDataString(CallbackUrl)}" +
            $"&openid.realm={Uri.EscapeDataString("http://localhost:7777/rift/")}" +
            $"&openid.identity={Uri.EscapeDataString("http://specs.openid.net/auth/2.0/identifier_select")}" +
            $"&openid.claimed_id={Uri.EscapeDataString("http://specs.openid.net/auth/2.0/identifier_select")}";

        private static void StopListener()
        {
            try { _listener?.Stop(); _listener?.Close(); _listener = null; }
            catch { }
        }
    }
}