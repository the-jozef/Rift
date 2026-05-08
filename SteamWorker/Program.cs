using Steamworks;
using System.Text.Json;

// Args: appId outputFile
if (args.Length < 2) return;

int appId = int.Parse(args[0]);
string outputFile = args[1];

// steam_appid.txt musí byť v CWD — je nastavený volajúcim procesom
if (!SteamAPI.Init())
{
    File.WriteAllText(outputFile, JsonSerializer.Serialize(new { Error = "Init failed" }));
    return;
}

bool statsOk = SteamUserStats.RequestCurrentStats();
if (!statsOk)
{
    SteamAPI.Shutdown();
    File.WriteAllText(outputFile, JsonSerializer.Serialize(new { Achievements = Array.Empty<object>() }));
    return;
}

// Počkaj na Steam
await Task.Delay(1000);
SteamAPI.RunCallbacks();

uint achCount = SteamUserStats.GetNumAchievements();
var achievements = new List<object>();
int unlocked = 0;

for (uint i = 0; i < achCount; i++)
{
    var apiName = SteamUserStats.GetAchievementName(i);
    var displayName = SteamUserStats.GetAchievementDisplayAttribute(apiName, "name");
    var description = SteamUserStats.GetAchievementDisplayAttribute(apiName, "desc");
    var iconNormal = SteamUserStats.GetAchievementDisplayAttribute(apiName, "icon");
    var iconGray = SteamUserStats.GetAchievementDisplayAttribute(apiName, "icon_gray");

    SteamUserStats.GetAchievementAndUnlockTime(apiName, out bool achieved, out uint unlockTime);
    if (achieved) unlocked++;

    achievements.Add(new
    {
        ApiName = apiName,
        Name = displayName,
        Description = description,
        IconUrl = !string.IsNullOrEmpty(iconNormal)
            ? $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{appId}/{iconNormal}"
            : "",
        IconGrayUrl = !string.IsNullOrEmpty(iconGray)
            ? $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{appId}/{iconGray}"
            : "",
        Unlocked = achieved,
        UnlockTime = unlockTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).UtcDateTime
            : (DateTime?)null
    });
}

var result = new { Achievements = achievements, Total = achievements.Count, Unlocked = unlocked };
File.WriteAllText(outputFile, JsonSerializer.Serialize(result));

SteamAPI.Shutdown();