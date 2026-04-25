using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App.Authorization
{
    public partial class SteamConnection : UserControl
    {
        private readonly SteamConnectionViewModel _viewModel;

        public SteamConnection()
        {
            InitializeComponent();

            _viewModel = new SteamConnectionViewModel();
            DataContext = _viewModel;

            // Steam connected → go to Register with steamId + name
            _viewModel.OnSteamConnected += (steamId, steamName) =>
            {
                try { ViewNavigator.Instance?.ShowRegister(steamId, steamName); }
                catch { }
            };

            // Return → go back to Login
            _viewModel.OnGoToLogin += () =>
            {
                try
                {
                    SteamAuthService.Cancel();
                    ViewNavigator.Instance?.ShowLogin();
                }
                catch { }
            };

            // Close X → go to AccountSelection
            _viewModel.OnGoToAccountSelection += () =>
            {
                try
                {
                    SteamAuthService.Cancel();
                    ViewNavigator.Instance?.ShowAccountSelection();
                }
                catch { }
            };
        }
    }

    // ─── VIEWMODEL ────────────────────────────────────────────────────────────

    public partial class SteamConnectionViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isConnecting = false;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _hasError = false;

        public event Action<string, string>? OnSteamConnected;
        public event Action? OnGoToLogin;
        public event Action? OnGoToAccountSelection;

        // ─── CONNECT STEAM ────────────────────────────────────────────────

        /// <summary>
        /// If clicked multiple times — cancels previous and starts new.
        /// Ak sa klikne viackrát — predchádzajúce zruší a spustí nové.
        /// </summary>
        [RelayCommand]
        private async Task ShowRegisterAsync()
        {
            try
            {
                // Cancel previous connection — zrušíme predchádzajúce pripojenie
                SteamAuthService.Cancel();

                IsConnecting = true;
                HasError = false;
                StatusMessage = "Waiting for Steam login...";

                // Small delay so previous cancel completes
                await Task.Delay(300);

                var steamId = await SteamAuthService.LoginAsync();

                if (string.IsNullOrEmpty(steamId))
                {
                    HasError = true;
                    StatusMessage = "Login cancelled. You can try again.";
                    return;
                }

                StatusMessage = "Connected! Loading your profile...";

                // Check if Steam account already has Rift account
                var existing = await ApiService.LoginSteamAsync(steamId);
                if (existing != null && existing.Success)
                {
                    HasError = true;
                    StatusMessage = "This Steam account already has a Rift account. Please login instead.";
                    return;
                }

                // Get Steam name to pre-fill in Register
                var playerInfo = await ApiService.GetPlayerInfoAsync(steamId);
                var steamName = playerInfo?.Username ?? string.Empty;

                OnSteamConnected?.Invoke(steamId, steamName);
            }
            catch
            {
                HasError = true;
                StatusMessage = "Something went wrong. Please try again.";
            }
            finally
            {
                IsConnecting = false;
            }
        }

        [RelayCommand]
        private void ShowLogin() => OnGoToLogin?.Invoke();

        [RelayCommand]
        private void ShowAccountSelection() => OnGoToAccountSelection?.Invoke();
    }
}
