using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace Rift_App
{
    public class ConfigService
    {
        private static readonly string _configFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MySteamApp");

        private static readonly string _configPath = Path.Combine(_configFolder, "appsettings.json");

        public string SteamApiKey { get; private set; } = string.Empty;

        public ConfigService()
        {
            // Vytvor priečinok, ak neexistuje
            Directory.CreateDirectory(_configFolder);

            if (!File.Exists(_configPath))
            {
                // Prvý štart – vytvorime prázdny súbor s inštrukciou
                var defaultConfig = new { Steam = new { ApiKey = "SEM_VLOZ_TVOJ_STEAM_API_KLUC_TU" } };
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));

                // Môžeš tu otvoriť MessageBox, aby používateľ vedel, čo má urobiť
                System.Windows.MessageBox.Show(
                    $"Konfiguračný súbor bol vytvorený tu:\n{_configPath}\n\n" +
                    "Otvor ho v Notepade a vlož svoj Steam API kľúč!",
                    "Prvý štart", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }

            // Načítaj kľúč
            var json = File.ReadAllText(_configPath);
            dynamic config = JsonConvert.DeserializeObject(json);
            SteamApiKey = config.Steam.ApiKey;

            if (string.IsNullOrWhiteSpace(SteamApiKey))
            {
                throw new Exception("Steam API kľúč nie je nastavený v appsettings.json!");
            }
        }
    }
}
    
