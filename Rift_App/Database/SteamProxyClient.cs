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
                BaseAddress = new Uri("https://rift-production-3468.up.railway.app/")   // Change this port to match your running SteamProxyBackend
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
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}
