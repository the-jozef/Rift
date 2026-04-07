using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Rift_App.Database;
using Rift_App.Login_Register;
using Rift_App.Services;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
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

                // === OVERENIE OPENID (najdôležitejšia časť) ===
                bool isValid = await VerifyOpenIdAsync(qs);
                if (!isValid)
                {
                    listener.Stop();
                    MessageBox.Show("Overenie Steam prihlásenia zlyhalo (možno falšovaná požiadavka).", "Chyba");
                    return;
                }

                // Vyťaženie SteamID64 (bezpečne po overení)
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
                string html = "<html><body style='background:#1A1B1F;color:white;font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'><h2>Successful sign up.<br>You can close this window and return to the RIFT App..</h2></body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();

                listener.Stop();

                // === Tvoj pôvodný kód pokračuje ===
                var steamService = new SteamService();
                string json = await steamService.GetPlayerSummary(steamId64);   // tu máš už v0002

                var jObj = JObject.Parse(json);
                var player = jObj["response"]?["players"]?[0];
                string steamName = player?["personaname"]?.ToString() ?? "Unknown";
                string steamAvatar = player?["avatarfull"]?.ToString() ?? "";

                bool exists = _authService.LoginWithSteam(steamId64, out string existingUsername, out string _);

                if (exists)
                {
                    CurrentSteamID64 = ulong.Parse(steamId64);
                    CurrentSteamName = existingUsername;
                    CurrentSteamAvatar = steamAvatar;
                    IsLoggedIn = true;
                    SessionService.Save(steamId64);
                    MessageBox.Show($"Vitaj späť, {existingUsername}!", "Prihlásenie úspešné");
                    // TODO: otvor MainWindow
                }
                else
                {
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
        private async Task<bool> VerifyOpenIdAsync(System.Collections.Specialized.NameValueCollection qs)
        {
            try
            {
                var values = new Dictionary<string, string>();

                // 1. Skopírujeme VŠETKY openid.* parametre (vrátane signed, sig, claimed_id atď.)
                foreach (var key in qs.AllKeys ?? Array.Empty<string>())
                {
                    if (key != null && key.StartsWith("openid."))
                    {
                        values[key] = qs[key] ?? string.Empty;
                    }
                }

                // 2. Potom PREPÍŠEME iba to, čo je potrebné pre overenie
                values["openid.ns"] = "http://specs.openid.net/auth/2.0";
                values["openid.mode"] = "check_authentication";

                var content = new FormUrlEncodedContent(values);
                var response = await _http.PostAsync("https://steamcommunity.com/openid/login", content);
                var responseText = await response.Content.ReadAsStringAsync();

                // Debug (ak by ešte stále nefungovalo – uvidíš čo presne Steam vrátil)
                // MessageBox.Show("Steam response:\n" + responseText, "Debug");

                return responseText.Contains("is_valid:true");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Chyba pri overovaní OpenID: " + ex.Message, "Chyba");
                return false;
            }
        }




        [RelayCommand]
        private void FinishSetup()
        {
            HasSetupError = false;

            if (string.IsNullOrWhiteSpace(SetupUsername) ||
                string.IsNullOrWhiteSpace(SetupPassword))
            {
                SetupError = "Vypln meno aj heslo!";
                HasSetupError = true;
                return;
            }

            if (SetupPassword.Length < 6)
            {
                SetupError = "Heslo musi mat aspon 6 znakov.";
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

                // Zapamätaj si posledného používateľa
                SessionService.Save(_tempSteamId64);

                MessageBox.Show($"Ucet vytvoreny! Vitaj, {SetupUsername}!", "Hotovo");
                // TODO: otvor MainWindow
            }
            else
            {
                SetupError = errorMsg;
                HasSetupError = true;
            }
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
            SessionService.Clear();
            IsLoggedIn = false;
            CurrentSteamName = string.Empty;
            CurrentSteamAvatar = string.Empty;
            CurrentSteamID64 = 0;
            MessageBox.Show("Odhlaseny!", "Logout");
        }
    }
}