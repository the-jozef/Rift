using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using Rift_App.Database;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace Rift_App.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService = new AuthService();
        private readonly HttpClient _http = new HttpClient();
        private static readonly string ApiKey = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .Build()["Steam:ApiKey"] ?? string.Empty;
        [ObservableProperty] private string currentSteamName = string.Empty;
        [ObservableProperty] private string currentSteamAvatar = string.Empty;
        [ObservableProperty] private ulong currentSteamID64;
        [ObservableProperty] private bool isLoggedIn;
        // Setup mod - novy hrac si vytvara Rift ucet
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
                // 1. Spusti lokalny listener
                using var listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:8080/callback/");
                listener.Start();
                // 2. Otvor Steam login v prehliadaci
                string returnTo = "http://localhost:8080/callback/";
                string steamUrl =
                "https://steamcommunity.com/openid/login" +
                "?openid.ns=http://specs.openid.net/auth/2.0" +
                "&openid.mode=checkid_setup" +
                $"&openid.return_to={Uri.EscapeDataString(returnTo)}" +
                $"&openid.realm={Uri.EscapeDataString("http://localhost:8080/")}" +
                "&openid.identity=" +
                "http://specs.openid.net/auth/2.0/identifier_select" +
                "&openid.claimed_id=" +
                "http://specs.openid.net/auth/2.0/identifier_select";
                Process.Start(new ProcessStartInfo(steamUrl) { UseShellExecute = true });
                // 3. Cakaj na callback (2 minuty timeout)
                var contextTask = listener.GetContextAsync();
                if (await Task.WhenAny(contextTask, Task.Delay(120_000)) != contextTask)

            {
                    listener.Stop();
                    MessageBox.Show("Prihlasenie vyprsalo. Skus znova.", "Timeout");
                    return;
                }
                var context = await contextTask;
                // 4. Posli odpoved do prehliadaca
                string html =
                "<html><body style='background:#1A1B1F;color:white;" +
                "font-family:sans-serif;display:flex;align-items:center;" +
                "justify-content:center;height:100vh;margin:0'>" +
                "<h2>Prihlasenie uspesne! Vrat sa do Rift App.</h2>" +
                "</body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer);
                context.Response.OutputStream.Close();
                listener.Stop();
                // 5. Vytiahneme SteamID64 z URL - DOLEZITE: UnescapeDataString!
                string fullUrl = context.Request.Url?.ToString() ?? "";
                string decodedUrl = Uri.UnescapeDataString(fullUrl);
                var match = Regex.Match(decodedUrl,
                @"openid\.claimed_id=https?://steamcommunity\.com/openid/id/(\d+)");
                if (!match.Success)
                {
                    MessageBox.Show("Nepodarilo sa ziskat Steam ID.", "Chyba");
                    return;
                }
                string steamId64 = match.Groups[1].Value;
                // 6. Nacitaj Steam profil
                string apiUrl =
                "https://api.steampowered.com/ISteamUser/" +
                $"GetPlayerSummaries/v2/?key={ApiKey}&steamids={steamId64}";
                string json = await _http.GetStringAsync(apiUrl);
                var jObj = JObject.Parse(json);
                var player = jObj["response"]?["players"]?[0];
                string steamName = player?["personaname"]?.ToString() ?? "Unknown";
                string steamAvatar = player?["avatarfull"]?.ToString() ?? "";
                // 7. Skontroluj ci ucet existuje v DB
                bool exists = _authService.LoginWithSteam(
                steamId64, out string existingUsername, out string _);
                if (exists)
                {
                    // Existujuci hrac -> prihlasenie
                    CurrentSteamID64 = ulong.Parse(steamId64);
                    CurrentSteamName = existingUsername;
                    CurrentSteamAvatar = steamAvatar;
                    IsLoggedIn = true;
                    MessageBox.Show($"Vitaj spat, {existingUsername}!", "Prihlasenie uspesne");
                    // TODO: otvor MainWindow
                }
                else
                {
                    // Novy hrac -> setup mod
                _tempSteamId64 = steamId64;
                    CurrentSteamName = steamName;
                    SetupUsername = steamName;
                    IsSetupMode = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba: {ex.Message}", "Chyba pri Steam prihlaseni");
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
    }
}