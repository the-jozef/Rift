using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using Rift_App.Models;
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
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasError = false;

        public event Action? OnLoginSuccess;
        public event Action? OnGoToRegister;
        public event Action? OnGoBack;

        [RelayCommand]
        private async Task LoginRiftAsync()
        {
            if (!ValidateRiftInput()) return;
            IsLoading = true; ClearError();
            try
            {
                var result = await ApiService.LoginRiftAsync(Username, Password);
                if (result == null) { ShowError("Could not connect to server. Try again."); return; }
                if (!result.Success) { ShowError(result.Message); return; }
                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                OnLoginSuccess?.Invoke();
            }
            catch { ShowError("Unexpected error. Please try again."); }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task LoginSteamAsync()
        {
            IsLoading = true; ClearError();
            try
            {
                var steamId = await SteamAuthService.LoginAsync();
                if (string.IsNullOrEmpty(steamId)) { ShowError("Steam login was cancelled. Please try again."); return; }
                var result = await ApiService.LoginSteamAsync(steamId);
                if (result == null) { ShowError("Could not connect to server. Try again."); return; }
                if (!result.Success) { ShowError("No Rift account found. Please register first."); return; }
                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                OnLoginSuccess?.Invoke();
            }
            catch { ShowError("Unexpected error. Please try again."); }
            finally { IsLoading = false; }
        }

        [RelayCommand] private void GoToRegister() => OnGoToRegister?.Invoke();
        [RelayCommand] private void GoBack() => OnGoBack?.Invoke();

        private bool ValidateRiftInput()
        {
            if (string.IsNullOrWhiteSpace(Username)) { ShowError("Please enter your username."); return false; }
            if (string.IsNullOrWhiteSpace(Password)) { ShowError("Please enter your password."); return false; }
            return true;
        }
        private void ShowError(string msg) { ErrorMessage = msg; HasError = true; }
        private void ClearError() { ErrorMessage = string.Empty; HasError = false; }
    }
}