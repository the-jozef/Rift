using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Services;
using System.Collections.ObjectModel;

namespace Rift_App.ViewModels
{
    public partial class AccountSelectionViewModel : ObservableObject
    {
        public ObservableCollection<AccountInfo> Accounts { get; } = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _hasError = false;

        public event Action? OnAccountSelected;
        public event Action? OnGoToLogin;

        [RelayCommand]
        public async Task LoadAccountsAsync()
        {
            IsLoading = true; ClearError(); Accounts.Clear();
            try
            {
                var accounts = await ApiService.GetDeviceAccountsAsync();
                if (accounts == null || accounts.Count == 0) { OnGoToLogin?.Invoke(); return; }

                foreach (var account in accounts)
                {
                    var info = new AccountInfo
                    {
                        UserId = account.UserId,
                        Username = account.Username,
                        SteamId64 = account.SteamId64,
                        LastLocation = account.LastLocation,
                        LastLoginAt = account.LastLoginAt,
                        IsLastActive = account.IsLastActive
                    };
                    var playerInfo = await ApiService.GetPlayerInfoAsync(account.SteamId64);
                    if (playerInfo != null) info.AvatarUrl = playerInfo.AvatarUrl;
                    Accounts.Add(info);
                }
            }
            catch { ShowError("Could not load accounts. Check your internet connection."); }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task SelectAccountAsync(AccountInfo account)
        {
            if (account == null) return;
            IsLoading = true; ClearError();
            try
            {
                SessionManager.SetSession(account.UserId, account.Username, account.SteamId64, account.LastLocation);
                await ApiService.SaveSessionAsync(account.LastLocation);
                OnAccountSelected?.Invoke();
            }
            catch { ShowError("Could not select account. Try again."); }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task RemoveFromDeviceAsync(AccountInfo account)
        {
            if (account == null) return;
            try
            {
                var success = await ApiService.RemoveAccountFromDeviceAsync(account.UserId);
                if (success) Accounts.Remove(account);
                else ShowError("Could not remove account. Try again.");
            }
            catch { ShowError("Unexpected error. Try again."); }
        }

        [RelayCommand]
        public async Task DeleteAccountFullyAsync(AccountInfo account)
        {
            if (account == null) return;
            try
            {
                var success = await ApiService.DeleteAccountFullyAsync(account.UserId);
                if (success) Accounts.Remove(account);
                else ShowError("Could not delete account. Try again.");
            }
            catch { ShowError("Unexpected error. Try again."); }
        }

        [RelayCommand] private void GoToLogin() => OnGoToLogin?.Invoke();
        private void ShowError(string msg) { ErrorMessage = msg; HasError = true; }
        private void ClearError() { ErrorMessage = string.Empty; HasError = false; }
    }

    // ─── AccountInfo model used in AccountSelection ───────────────────────────

    public class AccountInfo
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string SteamId64 { get; set; } = string.Empty;
        public string LastLocation { get; set; } = "Store";
        public DateTime LastLoginAt { get; set; }
        public bool IsLastActive { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;
        public string LastLoginDisplay
        {
            get
            {
                var diff = DateTime.UtcNow - LastLoginAt;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} min ago";
                if (diff.TotalDays < 1) return "Today";
                if (diff.TotalDays < 2) return "Yesterday";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
                return LastLoginAt.ToString("dd.MM.yyyy");
            }
        }
    }
}