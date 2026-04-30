using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Authorization;
using Rift_App.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Rift_App.Loading;
using Rift_App.Models;
using Rift_App.ViewModels;
using System.Linq;
using System.Net.Http.Headers;
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

        // ─── CURRENT VIEW — switches between UserControls ─────────────────
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
        [ObservableProperty] private string _registerConfirmPassword = string.Empty;
        [ObservableProperty] private bool _isSteamConnected = false;
        private string _steamId64 = string.Empty;

        // ─── STEAM CONNECTION FIELDS ──────────────────────────────────────
        [ObservableProperty] private string _steamStatusMessage = string.Empty;
        [ObservableProperty] private bool _steamHasError = false;
        [ObservableProperty] private bool _isConnecting = false;

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

        // ─── PRE-FILL REGISTER — called after Steam connect ───────────────

        public void PreFillRegister(string steamId, string steamName)
        {
            _steamId64 = steamId;
            IsSteamConnected = true;
            if (!string.IsNullOrEmpty(steamName))
                RegisterUsername = steamName;
            ShowRegister();
        }

        // ─── LOGIN — Rift username + password ─────────────────────────────

        [RelayCommand]
        private async Task LoginRiftAsync()
        {
            if (string.IsNullOrWhiteSpace(Username)) { ShowError("Please enter your username."); return; }
            if (string.IsNullOrWhiteSpace(Password)) { ShowError("Please enter your password."); return; }

            IsLoading = true;
            ClearErrors();

            try
            {
                var result = await ApiService.LoginRiftAsync(Username, Password);

                if (result == null || !result.Success)
                {
                    ShowError(result?.Message ?? "Could not connect to server.");
                    return;
                }

                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                ViewNavigator.Instance?.ShowLoading();
            }
            catch { ShowError("Unexpected error. Please try again."); }
            finally { IsLoading = false; }
        }

        // ─── LOGIN — Steam ─────────────────────────────────────────────────

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
                    ShowError("Steam login was cancelled. Please try again.");
                    return;
                }

                var result = await ApiService.LoginSteamAsync(steamId);

                if (result == null || !result.Success)
                {
                    ShowError("No Rift account found. Please register first.");
                    return;
                }

                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                ViewNavigator.Instance?.ShowLoading();
            }
            catch { ShowError("Unexpected error. Please try again."); }
            finally { IsLoading = false; }
        }

        // ─── STEAM CONNECT — for Register ────────────────────────────────

        [RelayCommand]
        private async Task ConnectSteamAsync()
        {
            SteamAuthService.Cancel();

            IsConnecting = true;
            SteamHasError = false;
            SteamStatusMessage = "Waiting for Steam login...";

            try
            {
                await Task.Delay(300);

                var steamId = await SteamAuthService.LoginAsync();

                if (string.IsNullOrEmpty(steamId))
                {
                    SteamHasError = true;
                    SteamStatusMessage = "Login cancelled. You can try again.";
                    return;
                }

                // ─── Prepne fokus späť na Rift apku ──────────────────────
                // Focus back to Rift window after Steam browser closes
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
                });

                SteamStatusMessage = "Connected! Loading your profile...";

                // Skontroluj ci uz ma Rift ucet
                // Check if Steam account already has a Rift account
                var existing = await ApiService.LoginSteamAsync(steamId);
                if (existing != null && existing.Success)
                {
                    SteamHasError = true;
                    SteamStatusMessage = "This Steam account already has a Rift account. Please login.";
                    return;
                }

                var playerInfo = await ApiService.GetPlayerInfoAsync(steamId);

                // Presunie na Register a predvyplni meno zo Steamu
                // Navigate to Register and pre-fill Steam username
                PreFillRegister(steamId, playerInfo?.Username ?? string.Empty);
            }
            catch
            {
                SteamHasError = true;
                SteamStatusMessage = "Something went wrong. Please try again.";
            }
            finally { IsConnecting = false; }
        }

        // ─── REGISTER ─────────────────────────────────────────────────────

        [RelayCommand]
        private async Task RegisterAsync()
        {
            if (string.IsNullOrWhiteSpace(RegisterUsername)) { ShowError("Please enter a username."); return; }
            if (RegisterUsername.Length < 3) { ShowError("Username must be at least 3 characters."); return; }
            if (string.IsNullOrWhiteSpace(RegisterPassword)) { ShowError("Please enter a password."); return; }
            if (RegisterPassword.Length < 6) { ShowError("Password must be at least 6 characters."); return; }
            if (RegisterPassword != RegisterConfirmPassword) { ShowError("Passwords do not match."); return; }
            if (string.IsNullOrEmpty(_steamId64)) { ShowError("Please connect your Steam account first."); return; }

            IsLoading = true;
            ClearErrors();

            try
            {
                var result = await ApiService.RegisterAsync(RegisterUsername, RegisterPassword, _steamId64);

                if (result == null || !result.Success)
                {
                    ShowError(result?.Message ?? "Could not connect to server.");
                    return;
                }

                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                ViewNavigator.Instance?.ShowLoading();
            }
            catch { ShowError("Unexpected error. Please try again."); }
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