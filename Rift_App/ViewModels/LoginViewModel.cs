using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using Rift_App.Database;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService = new AuthService();
        private readonly HttpClient _http = new HttpClient();

        [ObservableProperty] private string currentSteamName = string.Empty;
        [ObservableProperty] private string currentSteamAvatar = string.Empty;
        [ObservableProperty] private ulong currentSteamID64;
        [ObservableProperty] private bool isLoggedIn;
        [ObservableProperty] private bool isSetupMode;
        [ObservableProperty] private string setupUsername = string.Empty;
        [ObservableProperty] private string setupPassword = string.Empty;
        [ObservableProperty] private string setupError = string.Empty;
        [ObservableProperty] private bool hasSetupError;

        private string _tempSteamId64 = string.Empty;

        public LoginViewModel()
        {
            // Žiadne automatické prihlásenie pri štarte
        }

        private void AskForAutoLogin(string username, string plainPassword)
        {
            var result = MessageBox.Show(
                $"Chcete zapnúť automatické prihlásenie pre hráča **{username}**?\n\n" +
                "Pri ďalšom spustení sa vám vyplnia polia Account name a Password.",
                "Automatické prihlásenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _authService.SaveRememberedCredentials(username, plainPassword);
            }
        }

        [RelayCommand]
        private async Task SteamLogin()
        {
            try
            {
                using var listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:8080/callback/");
                listener.Start();

                string returnTo = "http://localhost:8080/callback/";
                string realm = "http://localhost:8080/";

                string steamUrl = "https://steamcommunity.com/openid/login" +
                    "?openid.ns=http://specs.openid.net/auth/2.0" +
                    "&openid.mode=checkid_setup" +
                    $"&openid.return_to={Uri.EscapeDataString(returnTo)}" +
                    $"&openid.realm={Uri.EscapeDataString(realm)}" +
                    "&openid.identity=http://specs.openid.net/auth/2.0/identifier_select" +
                    "&openid.claimed_id=http://specs.openid.net/auth/2.0/identifier_select";

                Process.Start(new ProcessStartInfo(steamUrl) { UseShellExecute = true });

                // Čakáme na callback (max 2 minúty)
                var contextTask = listener.GetContextAsync();
                if (await Task.WhenAny(contextTask, Task.Delay(120_000)) != contextTask)
                {
                    listener.Stop();
                    MessageBox.Show("Prihlásenie vypršalo. Skús znova.", "Timeout");
                    return;
                }

                var context = await contextTask;
                var qs = context.Request.QueryString;

                // Overenie OpenID
                bool isValid = await VerifyOpenIdAsync(qs);
                if (!isValid)
                {
                    listener.Stop();
                    MessageBox.Show("Overenie Steam prihlásenia zlyhalo.", "Chyba");
                    return;
                }

                // Získanie SteamID64
                string claimedId = qs["openid.claimed_id"] ?? "";
                var match = Regex.Match(claimedId, @"https?://steamcommunity\.com/openid/id/(\d{17})");
                if (!match.Success)
                {
                    listener.Stop();
                    MessageBox.Show("Nepodarilo sa získať Steam ID.", "Chyba");
                    return;
                }

                string steamId64 = match.Groups[1].Value;

                // Odpoveď prehliadaču
                string html = "<html><body style='background:#1A1B1F;color:white;font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'><h2>Prihlásenie úspešné.<br>Môžete zavrieť toto okno a vrátiť sa do RIFT App.</h2></body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
                listener.Stop();

                // Načítanie údajov zo Steamu
                var steamService = new SteamService();
                string json = await steamService.GetPlayerSummary(steamId64);
                var jObj = JObject.Parse(json);
                var player = jObj["response"]?["players"]?[0];

                string steamName = player?["personaname"]?.ToString() ?? "Unknown";
                string steamAvatar = player?["avatarfull"]?.ToString() ?? "";

                // Skontrolujeme, či účet už existuje
                bool exists = _authService.LoginWithSteam(steamId64, out string existingUsername, out string _);

                if (exists)
                {
                    // === EXISTUJÚCI ÚČET ===
                    CurrentSteamID64 = ulong.Parse(steamId64);
                    CurrentSteamName = existingUsername;
                    CurrentSteamAvatar = steamAvatar;
                    IsLoggedIn = true;

                    MessageBox.Show($"Vitaj späť, {existingUsername}!", "Prihlásenie úspešné");
                    // Pre existujúci Steam účet sa NEPÝTAME na auto-login (nemáme plain heslo)
                }
                else
                {
                    // === NOVÝ ÚČET - prejdeme do setup módu ===
                    _tempSteamId64 = steamId64;
                    CurrentSteamName = steamName;
                    SetupUsername = steamName;
                    IsSetupMode = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba pri Steam prihlásení: {ex.Message}", "Chyba");
            }
        }
        [RelayCommand]
        private void FinishSetup()
        {
            HasSetupError = false;
            if (string.IsNullOrWhiteSpace(SetupUsername) || string.IsNullOrWhiteSpace(SetupPassword))
            {
                SetupError = "Vyplň meno aj heslo!";
                HasSetupError = true;
                return;
            }
            if (SetupPassword.Length < 6)
            {
                SetupError = "Heslo musí mať aspoň 6 znakov.";
                HasSetupError = true;
                return;
            }

            bool success = _authService.RegisterWithSteam(
                SetupUsername, SetupPassword, _tempSteamId64, out string errorMsg);

            if (success)
            {
                CurrentSteamID64 = ulong.Parse(_tempSteamId64);
                CurrentSteamName = SetupUsername;
                IsLoggedIn = true;
                IsSetupMode = false;

                AskForAutoLogin(SetupUsername, SetupPassword);   // ← tu sa pýta a ukladá

                MessageBox.Show($"Účet vytvorený! Vitaj, {SetupUsername}!", "Hotovo");
            }
            else
            {
                SetupError = errorMsg;
                HasSetupError = true;
            }
        }

        // Stub pre normal login (aby XAML nepadal)
        [RelayCommand]
        private void SignIn()
        {
            // Tu môžeš neskôr implementovať normálne prihlásenie
            MessageBox.Show("Normálne prihlásenie ešte nie je plne implementované v tejto verzii.", "Info");
        }

        [RelayCommand]
        private void CancelSetup()
        {
            IsSetupMode = false;
            _tempSteamId64 = string.Empty;
            SetupUsername = string.Empty;
            SetupPassword = string.Empty;
        }

        [RelayCommand]
        private void Logout()
        {
            IsLoggedIn = false;
            CurrentSteamName = string.Empty;
            CurrentSteamAvatar = string.Empty;
            CurrentSteamID64 = 0;
            MessageBox.Show("Bol si odhlásený.", "Logout");
        }

        // VerifyOpenIdAsync zostáva rovnaký ako mal si predtým...
        private async Task<bool> VerifyOpenIdAsync(System.Collections.Specialized.NameValueCollection qs)
        {
            try
            {
                var values = new Dictionary<string, string>();
                foreach (var key in qs.AllKeys ?? Array.Empty<string>())
                {
                    if (key?.StartsWith("openid.") == true)
                        values[key] = qs[key] ?? string.Empty;
                }
                values["openid.ns"] = "http://specs.openid.net/auth/2.0";
                values["openid.mode"] = "check_authentication";

                var content = new FormUrlEncodedContent(values);
                var response = await _http.PostAsync("https://steamcommunity.com/openid/login", content);
                var responseText = await response.Content.ReadAsStringAsync();
                return responseText.Contains("is_valid:true");
            }
            catch
            {
                return false;
            }
        }
    }
}