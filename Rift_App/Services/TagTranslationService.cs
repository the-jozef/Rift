using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.Services
{
    public static class TagTranslationService
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["sk"] = new()
            {
                ["Action"] = "Akčné",
                ["Adventure"] = "Dobrodružné",
                ["RPG"] = "RPG",
                ["Strategy"] = "Stratégické",
                ["Simulation"] = "Simulátor",
                ["Sports"] = "Športové",
                ["Racing"] = "Pretekárske",
                ["Horror"] = "Hororové",
                ["Puzzle"] = "Logické",
                ["Platformer"] = "Plošinové",
                ["Shooter"] = "Strieľačky",
                ["Indie"] = "Indie",
                ["Casual"] = "Oddychové",
                ["Multiplayer"] = "Pre viac hráčov",
                ["Single-player"] = "Pre jedného hráča",
                ["Co-op"] = "Kooperatívne",
                ["Online Co-op"] = "Online kooperácia",
                ["PvP"] = "PvP",
                ["Online PvP"] = "Online PvP",
                ["Survival"] = "Prežitie",
                ["Open World"] = "Otvorený svet",
                ["Story Rich"] = "Príbehové",
                ["Atmospheric"] = "Atmosferické",
                ["Early Access"] = "Predbežný prístup",
                ["Free to Play"] = "Zadarmo",
                ["Massively Multiplayer"] = "Masívne multiplayerové",
                ["Tower Defense"] = "Obrana veží",
                ["Turn-Based"] = "Ťahové",
                ["Real-Time"] = "V reálnom čase",
                ["Fighting"] = "Bojové",
                ["Stealth"] = "Stealthové",
                ["Sandbox"] = "Sandboxové",
                ["Building"] = "S budovaním",
                ["Crafting"] = "S vyrábaním",
                ["Exploration"] = "Prieskumové",
                ["Anime"] = "Anime",
                ["Sci-fi"] = "Sci-fi",
                ["Fantasy"] = "Fantasy",
                ["Historical"] = "Historické",
            }
        };

        public static string Translate(string tag)
        {
            var lang = LanguageService.CurrentLanguage;

            // Ak sme v EN alebo jazyk nemá preklad → vráť originál
            if (lang == "en") return tag;

            if (_translations.TryGetValue(lang, out var dict) &&
                dict.TryGetValue(tag, out var translated))
                return translated;

            // Neznámy tag → vráť originál
            return tag;
        }
    }
}