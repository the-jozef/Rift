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
                ["Action"] = "Akcia",
                ["Adventure"] = "Dobrodružstvo",
                ["RPG"] = "RPG",
                ["Strategy"] = "Stratégia",
                ["Simulation"] = "Simulácia",
                ["Sports"] = "Šport",
                ["Racing"] = "Preteky",
                ["Horror"] = "Horor",
                ["Puzzle"] = "Hádanky",
                ["Platformer"] = "Plošinová",
                ["Shooter"] = "Strieľačka",
                ["Indie"] = "Indie",
                ["Casual"] = "Casual",
                ["Multiplayer"] = "Multiplayer",
                ["Single-player"] = "Pre jedného hráča",
                ["Co-op"] = "Kooperácia",
                ["Online Co-op"] = "Online kooperácia",
                ["PvP"] = "Hráč vs hráč",
                ["Online PvP"] = "Online PvP",
                ["Survival"] = "Prežitie",
                ["Open World"] = "Otvorený svet",
                ["Story Rich"] = "Príbehová",
                ["Atmospheric"] = "Atmosferická",
                ["Early Access"] = "Skorý prístup",
                ["Free to Play"] = "Zadarmo",
                ["Massively Multiplayer"] = "Masové multiplayer",
                ["Tower Defense"] = "Obrana veže",
                ["Turn-Based"] = "Ťahová",
                ["Real-Time"] = "V reálnom čase",
                ["Fighting"] = "Bojová",
                ["Stealth"] = "Nenápadnosť",
                ["Sandbox"] = "Sandbox",
                ["Building"] = "Budovanie",
                ["Crafting"] = "Výroba",
                ["Exploration"] = "Prieskum",
                ["Anime"] = "Anime",
                ["Sci-fi"] = "Sci-fi",
                ["Fantasy"] = "Fantasy",
                ["Historical"] = "Historická",
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