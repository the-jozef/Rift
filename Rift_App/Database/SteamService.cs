using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rift_App.Database
{
    public class SteamService
    {
        private readonly SteamProxyClient _proxy = new SteamProxyClient();

        public async Task<string> GetPlayerSummary(string steamId64)
        {
            var parameters = new Dictionary<string, string>
            {
                { "steamids", steamId64 }
            };

            return await _proxy.CallSteam("ISteamUser", "GetPlayerSummaries", "v0002", parameters);
        }
    }
}