using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.IO;

namespace Rift_App.Services
{
    public static class LanguageService
    {
        //Catch current language — to know if we need to switch
        public static string CurrentLanguage { get; private set; } = "en";

        public static CultureInfo Current { get; private set; } = CultureInfo.InvariantCulture;

        public static event Action? LanguageChanged;

        public static string LoadLanguage()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return "en";
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonConvert.DeserializeObject<dynamic>(json);
                return (string)(settings?.Language ?? "en");
            }
            catch { return "en"; }
        }

        public static void Switch(string lang)
        {
            //Save current language
            CurrentLanguage = lang;

            Current = lang switch
            {
                "sk" => new CultureInfo("sk-SK"),
                _ => CultureInfo.InvariantCulture
            };

            var merged = Application.Current.Resources.MergedDictionaries;
            var old = merged.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("Strings.") == true);

            if (old != null) merged.Remove(old);

            merged.Add(new ResourceDictionary
            {
                Source = new Uri($"/Languages/Strings.{lang}.xaml",UriKind.Relative)
            });

            //Save to file
            Save(lang);

            LanguageChanged?.Invoke();
        }

        private static readonly string SettingsPath = Path.Combine(AppPaths.Root, "settings.json");

        private static void Save(string lang)
        {
            try
            {
                AppPaths.Ensure(AppPaths.Root);
                File.WriteAllText(SettingsPath,
                    JsonConvert.SerializeObject(new { Language = lang }));
            }
            catch { }
        }
    }
}