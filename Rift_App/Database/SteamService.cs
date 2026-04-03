using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Database
{
    public class SteamService
    {
        private readonly HttpClient _client = new()
        {
            BaseAddress = new Uri("https://api.steampowered.com/")
        };

        private static readonly string _apiKey =
            new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build()["Steam:ApiKey"] ?? string.Empty;

        public async Task<string> GetPlayerSummary(string steamId64)
        {
            string url = $"ISteamUser/GetPlayerSummaries/v0002/?key={_apiKey}&steamids={steamId64}";
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
