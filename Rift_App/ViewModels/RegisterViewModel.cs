using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Rift_App.Authorization;
using Rift_App.Services;
using System;
using System.Windows.Threading;
using Rift_App.Loading;

namespace Rift_App.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {

        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private string _confirmPassword = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _hasError = false;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _isSteamConnected = false;
        [ObservableProperty] private object _currentView;

        private string _steamId64 = string.Empty;

        public event Action? OnRegisterSuccess;
        public event Action? OnGoToLogin;

        public WindowStateViewModel WindowState { get; }

        public event Action LoadingFinished;
        public RegisterViewModel()
        {
            WindowState = new WindowStateViewModel();
            CurrentView = new AccountSelection();
        }

        [RelayCommand]
        private async Task Loading()
        {
            var loading = new LoadingWindow();
            loading.ShowDialog();
            await Task.Delay(3000);
            LoadingFinished?.Invoke();
        }

        [RelayCommand]
        public void ShowLogin() => CurrentView = new Login();

        [RelayCommand]
        public void ShowSteamConnection() => CurrentView = new SteamConnection();

        [RelayCommand]
        private void ShowRegister() => CurrentView = new Register();

        [RelayCommand]
        private void ShowAccountSelection() => CurrentView = new AccountSelection();
        [RelayCommand]
        private void ShowLoading() => CurrentView = new Login();

        [RelayCommand]
        private async Task RegisterAsync()
        {
            if (!ValidateInput()) return;
            IsLoading = true; ClearError();
            try
            {
                var result = await ApiService.RegisterAsync(Username, Password, _steamId64);
                if (result == null) { ShowError("Could not connect to server. Try again."); return; }
                if (!result.Success) { ShowError(result.Message); return; }
                SessionManager.SetSession(result.UserId!.Value, result.Username!, result.SteamId64!);
                OnRegisterSuccess?.Invoke();
            }
            catch { ShowError("Unexpected error. Please try again."); }
            finally { IsLoading = false; }
        }

        [RelayCommand] private void GoToLogin() => OnGoToLogin?.Invoke();

        public void PreFillSteam(string steamId, string steamName)
        {
            try
            {
                _steamId64 = steamId;
                IsSteamConnected = true;
                if (!string.IsNullOrEmpty(steamName)) Username = steamName;
            }
            catch { }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(Username)) { ShowError("Please enter a username."); return false; }
            if (Username.Length < 3) { ShowError("Username must be at least 3 characters."); return false; }
            if (string.IsNullOrWhiteSpace(Password)) { ShowError("Please enter a password."); return false; }
            if (Password.Length < 6) { ShowError("Password must be at least 6 characters."); return false; }
            if (Password != ConfirmPassword) { ShowError("Passwords do not match."); return false; }
            if (string.IsNullOrEmpty(_steamId64)) { ShowError("Please connect your Steam account first."); return false; }
            return true;
        }
        private void ShowError(string msg) { ErrorMessage = msg; HasError = true; }
        private void ClearError() { ErrorMessage = string.Empty; HasError = false; }
    }
}