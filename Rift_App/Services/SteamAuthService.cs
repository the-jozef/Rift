using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rift_App.Services
{
    public partial class SteamAuthService : ObservableObject
    {
        private const string SteamOpenIdUrl = "https://steamcommunity.com/openid/login";
        private const string CallbackUrl = "http://localhost:7777/auth/";
        private const string ListenerPrefix = "http://localhost:7777/auth/";

        private static HttpListener? _listener;
        private static CancellationTokenSource? _cts;

        public static async Task<string?> LoginAsync()
        {
            StopListener();
            await Task.Delay(200);

            _cts = new CancellationTokenSource();

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(ListenerPrefix);

                try
                {
                    _listener.Start();
                }
                catch (HttpListenerException ex)
                {
                    Debug.WriteLine($"[Steam] Cannot start listener: {ex.Message}");
                    return null;
                }

                Debug.WriteLine("[Steam] Listener started on " + ListenerPrefix);

                var url = BuildSteamUrl();
                Debug.WriteLine("[Steam] Opening browser: " + url);

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeout.Token);

                return await WaitForCallbackAsync(linked.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steam] LoginAsync error: {ex.Message}");
                return null;
            }
            finally
            {
                StopListener();
            }
        }

        public static void Cancel()
        {
            try
            {
                _cts?.Cancel();
                StopListener();
            }
            catch { }
        }

        // FIX: contextTask sa vytvori raz — nie kazdy cyklus znova
        private static async Task<string?> WaitForCallbackAsync(CancellationToken token)
        {
            if (_listener == null) return null;

            try
            {
                var contextTask = _listener.GetContextAsync();

                while (!token.IsCancellationRequested)
                {
                    var completed = await Task.WhenAny(contextTask, Task.Delay(500, token));

                    if (completed == contextTask)
                    {
                        var context = await contextTask;
                        var fullUrl = context.Request.Url?.ToString() ?? "";

                        Debug.WriteLine("[Steam] Got callback: " + fullUrl);

                        await SendResponseAsync(context.Response);

                        return ExtractSteamId(fullUrl);
                    }
                }

                Debug.WriteLine("[Steam] Cancelled.");
                return null;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Steam] Timeout / cancelled.");
                return null;
            }
            catch (HttpListenerException ex)
            {
                Debug.WriteLine($"[Steam] Listener error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steam] Unexpected error: {ex.Message}");
                return null;
            }
        }

        private static async Task SendResponseAsync(HttpListenerResponse response)
        {
            try
            {
                var html = """
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Rift — Connected</title>
                        <style>
                            body { background: #1b2838; display: flex; justify-content: center;
                                   align-items: center; height: 100vh; margin: 0; font-family: Arial; }
                            h1 { color: #66c0f4; }
                            p { color: #8b929a; }
                        </style>
                    </head>
                    <body>
                        <div style="text-align:center">
                            <h1>Connected!</h1>
                            <p>Return to Rift. This window will close automatically.</p>
                        </div>
                        <script>setTimeout(() => window.close(), 1500);</script>
                    </body>
                    </html>
                    """;

                var bytes = System.Text.Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                response.StatusCode = 200;
                await response.OutputStream.WriteAsync(bytes);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steam] SendResponse error: {ex.Message}");
            }
        }

        private static string? ExtractSteamId(string url)
        {
            try
            {
                var match = Regex.Match(url, @"openid\.claimed_id=https?://steamcommunity\.com/openid/id/(\d{17})");
                if (match.Success)
                    return match.Groups[1].Value;

                var decoded = Uri.UnescapeDataString(url);
                match = Regex.Match(decoded, @"steamcommunity\.com/openid/id/(\d{17})");
                if (match.Success)
                    return match.Groups[1].Value;

                Debug.WriteLine("[Steam] Could not extract SteamID from: " + url);
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Steam] ExtractSteamId error: {ex.Message}");
                return null;
            }
        }

        private static string BuildSteamUrl()
        {
            var callbackEncoded = Uri.EscapeDataString(CallbackUrl);
            var realmEncoded = Uri.EscapeDataString("http://localhost:7777/");

            return $"{SteamOpenIdUrl}" +
                   $"?openid.ns={Uri.EscapeDataString("http://specs.openid.net/auth/2.0")}" +
                   $"&openid.mode=checkid_setup" +
                   $"&openid.return_to={callbackEncoded}" +
                   $"&openid.realm={realmEncoded}" +
                   $"&openid.identity={Uri.EscapeDataString("http://specs.openid.net/auth/2.0/identifier_select")}" +
                   $"&openid.claimed_id={Uri.EscapeDataString("http://specs.openid.net/auth/2.0/identifier_select")}";
        }

        private static void StopListener()
        {
            try
            {
                _listener?.Stop();
                _listener?.Close();
                _listener = null;
            }
            catch { }
        }
    }
}