using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Rift_App.Database
{
    public class SteamProxyClient
    {
        private readonly HttpClient _client;

        public SteamProxyClient()
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri("https://rift-hupv.onrender.com/"),
                Timeout = TimeSpan.FromSeconds(60) // Render cold start can be slow
            };
        }

        public async Task<string> CallSteam(string interfaceName, string methodName, Dictionary<string, string> parameters)
        {
            var requestData = new
            {
                Interface = interfaceName,
                Method = methodName,
                Version = "v0001",
                Parameters = parameters
            };

            var response = await _client.PostAsJsonAsync("api/steam", requestData);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || !content.TrimStart().StartsWith("{"))
                throw new Exception($"Proxy chyba (HTTP {(int)response.StatusCode}): {content[..Math.Min(300, content.Length)]}");

            return content;
        }
    }
}