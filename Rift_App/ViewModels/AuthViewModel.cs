using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Rift_App.Authorization;
using Rift_App.Languages;
using Rift_App.Loading;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rift_App.ViewModels
{
    public partial class AuthViewModel : ObservableObject
    {
        // ─── WINDOW STATE ─────────────────────────────────────────────────
        public WindowStateViewModel WindowState { get; } = new();

        // ─── CURRENT VIEW — switches between UserControls ────────────────
        [ObservableProperty]
        private object _currentView = null!;

        // ─── ERROR / LOADING ──────────────────────────────────────────────
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _hasError = false;
        [ObservableProperty] private bool _isLoading = false;

        // ─── LOGIN FIELDS ─────────────────────────────────────────────────
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _password = string.Empty;

        // ─── REGISTER FIELDS ─────────────────────────────────────────────
        [ObservableProperty] private string _registerUsername = string.Empty;
        [ObservableProperty] private string _registerPassword = string.Empty;
        [ObservableProperty] private bool _isSteamConnected = false;
        private string _steamId64 = string.Empty;

        // ─── STEAM CONNECTION FIELDS ──────────────────────────────────────
        [ObservableProperty] private string _steamStatusMessage = string.Empty;
        [ObservableProperty] private bool _steamHasError = false;
        [ObservableProperty] private bool _isConnecting = false;

        // ─── DEVICE ACCOUNTS — list of accounts on this device ───────────
        // List of accounts on this device
        public ObservableCollection<AccountInfo> DeviceAccounts { get; } = new();

        public AuthViewModel()
        {
            CurrentView = new AccountSelection();
        }

        // ─── NAVIGATION ───────────────────────────────────────────────────

        [RelayCommand]
        public void ShowAccountSelection()
        {
            ClearErrors();
            CurrentView = new AccountSelection();
            _ = LoadAccountsAsync(); // load accounts immediately
        }

        [RelayCommand]
        public void ShowLogin()
        {
            ClearErrors();
            CurrentView = new Login();
        }

        [RelayCommand]
        public void ShowSteamConnection()
        {
            ClearErrors();
            CurrentView = new SteamConnection();
        }

        [RelayCommand]
        public void ShowRegister()
        {
            ClearErrors();
            CurrentView = new Register();
        }

        // ─── LOAD DEVICE ACCOUNTS — called when AccountSelection opens ─────────
        public async Task LoadAccountsAsync()
        {
            try
            {
                var accounts = await ApiService.GetDeviceAccountsAsync();
                DeviceAccounts.Clear();
                foreach (var acc in accounts)
                {
                    if (string.IsNullOrEmpty(acc.AvatarUrl))
                    {
                        var player = await ApiService.GetPlayerInfoAsync(acc.SteamId64);
                        acc.AvatarUrl = player?.AvatarUrl ?? string.Empty;
                    }
                    DeviceAccounts.Add(acc);
                }
            }
            catch { }
        }

        // Select account — click on existing account tile
        [RelayCommand]
        private void SelectAccount(AccountInfo account)
        {
            if (account == null) return;
            SessionManager.SetSession(account.UserId, account.Username, account.SteamId64, account.LastLocation);
            SessionManager.NotifySessionReady();
            ViewNavigator.Instance?.ShowLoading();
        }

        // ─── REMOVE ACCOUNT — removes account from device ────────────────
        [RelayCommand]
        private async Task RemoveAccountAsync(AccountInfo account)
        {
            if (account == null) return;

            try
            {
                // 1. Remove device↔account link from backend
                await ApiService.RemoveAccountFromDeviceAsync(account.UserId);
                DeviceAccounts.Remove(account);
            }
            catch { }

            // 2. Delete all local data for this Steam account
            //    This includes library, achievements, wishlist, account snapshot, etc.
            if (!string.IsNullOrEmpty(account.SteamId64))
            {
                try
                {
                    var userFolder = AppPaths.User(account.SteamId64);
                    if (System.IO.Directory.Exists(userFolder))
                        System.IO.Directory.Delete(userFolder, recursive: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine( $"[AuthVM] Could not delete user folder: {ex.Message}");
                }
            }
        }

        // ─── PRE-FILL REGISTER — sets Steam username and switches to Register view ──────────────
        public void PreFillRegister(string steamId, string steamName)
        {
            _steamId64 = steamId;
            IsSteamConnected = true;
            RegisterUsername = steamName;
            ShowRegister();
        }

        // ─── LOGIN — Rift username + password ─────────────────────────────

        [RelayCommand]
        private async Task LoginRiftAsync()
        {
            ClearErrors();

            if (string.IsNullOrWhiteSpace(Username)) { ShowError(L.Get("err_username_empty")); return; }
            if (string.IsNullOrWhiteSpace(Password)) { ShowError(L.Get("err_password_empty")); return; }

            IsLoading = true;

            try
            {
                var result = await ApiService.LoginRiftAsync(Username, Password);

                if (result == null || !result.Success)
                {
                    ShowError(L.Get("err_invalid"));
                    return;
                }

                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                SessionManager.NotifySessionReady();
                ViewNavigator.Instance?.ShowLoading();
            }
            catch { ShowError(L.Get("err_could_not_connect")); }
            finally { IsLoading = false; }
        }

        // ─── LOGIN — Steam ─────────────────────────────────────────────────
        // If account does not exist — shows MessageBox

        [RelayCommand]
        private async Task LoginSteamAsync()
        {
            IsLoading = true;
            ClearErrors();

            try
            {
                var steamId = await SteamAuthService.LoginAsync();

                if (string.IsNullOrEmpty(steamId))
                {
                    ShowError(L.Get("err_steam_cancelled"));
                    return;
                }

                var result = await ApiService.LoginSteamAsync(steamId);

                if (result == null || !result.Success)
                {
                    MessageBox.Show(
     L.Get("err_account_not_found"),
     L.Get("err_account_not_found_title"),
     MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                SessionManager.NotifySessionReady();
                ViewNavigator.Instance?.ShowLoading();
            }
            catch { ShowError(L.Get("err_could_not_connect")); }
            finally { IsLoading = false; }
        }

        // ─── STEAM CONNECT — pre Register ────────────────────────────────

        [RelayCommand]
        private async Task ConnectSteamAsync()
        {
            SteamAuthService.Cancel();

            IsConnecting = true;
            SteamHasError = false;
            SteamStatusMessage = L.Get("steam_connect_waiting");

            try
            {
                await Task.Delay(300);

                var steamId = await SteamAuthService.LoginAsync();

                if (string.IsNullOrEmpty(steamId))
                {
                    SteamHasError = true;
                    SteamStatusMessage = "steam_connect_cancelled";
                    return;
                }

                SteamStatusMessage = L.Get("steam_connect_loading");

                var existing = await ApiService.LoginSteamAsync(steamId);
                if (existing != null && existing.Success)
                {
                    SteamHasError = true;
                    SteamStatusMessage = L.Get("steam_connect_already_exists");
                    return;
                }

                PlayerInfo? playerInfo = null;
                for (int i = 0; i < 3; i++)
                {
                    playerInfo = await ApiService.GetPlayerInfoAsync(steamId);
                    if (playerInfo != null) break;
                    await Task.Delay(3000);
                }

                // Everything that touches UI must run on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var authWindow = Application.Current.Windows
                        .OfType<AuthWindow>()
                        .FirstOrDefault();

                    if (authWindow != null)
                    {
                        authWindow.Activate();
                        authWindow.Topmost = true;
                        authWindow.Topmost = false;
                        authWindow.Focus();
                    }

                    PreFillRegister(steamId, playerInfo?.Username ?? string.Empty);
                });
            }
            catch
            {
                SteamHasError = true;
                SteamStatusMessage = L.Get("steam_connect_error");
            }
            finally { IsConnecting = false; }
        }

        // ─── REGISTER ─────────────────────────────────────────────────────

        [RelayCommand]
        private async Task RegisterAsync()
        {
            ClearErrors();

            if (string.IsNullOrWhiteSpace(RegisterUsername))
            {
                ShowError(L.Get("err_username_empty"));
                return;
            }
            if (RegisterUsername.Length < 3)
            {
                ShowError(L.Get("err_username_too_short"));
                return;
            }
            if (RegisterUsername.Length > 20)
            {
                ShowError(L.Get("err_username_too_long"));
                return;
            }
            if (RegisterUsername.Contains(' '))
            {
                ShowError(L.Get("err_username_contains_spaces"));
                return;
            }
            if (string.IsNullOrWhiteSpace(RegisterPassword))
            {
                ShowError(L.Get("err_password_empty"));
                return;
            }
            if (RegisterPassword.Length < 6)
            {
                ShowError(L.Get("err_password_too_short"));
                return;
            }
            if (RegisterPassword.Length > 40)
            {
                ShowError(L.Get("err_password_too_long"));
                return;
            }

            IsLoading = true;

            try
            {
                var result = await ApiService.RegisterAsync(RegisterUsername, RegisterPassword, _steamId64);

                if (result == null || !result.Success)
                {
                    ShowError(result?.Message ?? L.Get("err_could_not_connect"));
                    return;
                }

                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                SessionManager.NotifySessionReady();
                ViewNavigator.Instance?.ShowLoading();
            }
            catch { ShowError(L.Get("err_could_not_connect")); }
            finally { IsLoading = false; }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────
        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        private void ClearErrors()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }
    }
}