using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class SearchBarViewModel : ObservableObject
    {
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isSearching = false;
        [ObservableProperty] private bool _showDropdown = false;
        [ObservableProperty] private int _wishlistCount = 0;

        public ObservableCollection<SearchResultModel> Results { get; } = new();

        private CancellationTokenSource? _debounceCts;
        private const int DebounceMs = 320;

        // ─── SEARCH ───────────────────────────────────────────────────────
        public async Task OnSearchTextChangedAsync(string text)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
            {
                ShowDropdown = false;
                Results.Clear();
                return;
            }

            try
            {
                await Task.Delay(DebounceMs, token);
                if (token.IsCancellationRequested) return;

                IsSearching = true;
                ShowDropdown = true;

                var results = await ApiService.SearchGamesAsync(text);
                if (token.IsCancellationRequested) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Results.Clear();
                    foreach (var r in results) Results.Add(r);
                    ShowDropdown = Results.Count > 0;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchBar] Error: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
            }
        }

        // ─── COMMANDS ─────────────────────────────────────────────────────
        [RelayCommand]
        public void SelectResult(SearchResultModel result)
        {
            if (result == null) return;
            ShowDropdown = false;
            SearchText = string.Empty;
            Results.Clear();

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"steam://store/{result.AppId}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchBar] Open steam error: {ex.Message}");
            }
        }

        [RelayCommand]
        public void ClearSearch()
        {
            SearchText = string.Empty;
            ShowDropdown = false;
            Results.Clear();
        }

        [RelayCommand]
        public void ShowWishlist() =>
            ViewNavigator.Instance?.MainViewModel?.ShowWishlist();

        [RelayCommand]
        public void CloseDropdown() => ShowDropdown = false;

        // ─── WISHLIST COUNT — z centrálneho cache ─────────────────────────
        // No API call — just reads what WishlistVM or AccountVM already loaded
        public async Task LoadWishlistCountAsync()
        {
            WishlistCount = await WishlistCountCache.GetAsync();
        }

        // Call this after every wishlist change (add/remove)
        public void RefreshWishlistCount()
        {
            WishlistCount = WishlistCountCache.Count;
        }
    }
}