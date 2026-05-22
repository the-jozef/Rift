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
        [ObservableProperty] private bool _isRefreshing = false;

        public ObservableCollection<SearchResultModel> Results { get; } = new();

        private CancellationTokenSource? _debounceCts;
        private const int DebounceMs = 350;

        // ─── SEARCH ───────────────────────────────────────────────────────
        public async Task OnSearchTextChangedAsync(string text)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            if (string.IsNullOrWhiteSpace(text) || text.Length < 1)
            {
                ShowDropdown = false;
                Results.Clear();
                IsSearching = false;
                return;
            }

            try
            {
                await Task.Delay(DebounceMs, token);
                if (token.IsCancellationRequested) return;

                IsSearching = true;
                ShowDropdown = true;

                // 12-second cap so the spinner never hangs
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                using var linked = CancellationTokenSource
                    .CreateLinkedTokenSource(token, timeoutCts.Token);

                List<SearchResultModel> results;
                try
                {
                    results = await ApiService.SearchGamesAsync(text)
                                              .WaitAsync(linked.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    Debug.WriteLine("[SearchBar] Search timed out.");
                    results = new();
                }

                if (token.IsCancellationRequested) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Results.Clear();
                    foreach (var r in results) Results.Add(r);
                    ShowDropdown = Results.Count > 0;
                });
            }
            catch (OperationCanceledException) { /* user typed again — normal */ }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchBar] Error: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
            }
        }

        // ─── REFRESH ──────────────────────────────────────────────────────
        /// <summary>
        /// Clears volatile caches and shows the loading screen so library,
        /// wishlist and profile get fresh data. Store is NOT cleared (shared cache).
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;

            try
            {
                var steamId = SessionManager.SteamId64;

                // 1. Force LastPlayed re-read from VDF
                await LastPlayedCacheService.RefreshAsync();

                // 2. Invalidate account snapshot so profile re-fetches
                try
                {
                    var snap = System.IO.Path.Combine(
                        AppPaths.Account(steamId), "snapshot.json");
                    if (System.IO.File.Exists(snap))
                        System.IO.File.Delete(snap);
                }
                catch { }

                // 3. Wishlist count will re-fetch on next request
                WishlistCountCache.Invalidate();

                // 4. Show loading screen → navigates back to last location
                ViewNavigator.Instance?.ShowLoading();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchBar] Refresh error: {ex.Message}");
            }
            finally
            {
                // IsRefreshing stays true until loading screen takes over,
                // so the button stays dimmed (window hides during loading).
                // It resets to false when SearchBar is next constructed.
                IsRefreshing = false;
            }
        }

        // ─── OTHER COMMANDS ───────────────────────────────────────────────
        [RelayCommand]
        public void SelectResult(SearchResultModel result)
        {
            if (result == null) return;
            ShowDropdown = false;
            SearchText = string.Empty;
            Results.Clear();

            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
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
            _debounceCts?.Cancel();
            SearchText = string.Empty;
            ShowDropdown = false;
            IsSearching = false;
            Results.Clear();
        }

        [RelayCommand]
        public void ShowWishlist() =>
            ViewNavigator.Instance?.MainViewModel?.ShowWishlist();

        [RelayCommand]
        public void CloseDropdown() => ShowDropdown = false;

        // ─── WISHLIST COUNT ───────────────────────────────────────────────
        public async Task LoadWishlistCountAsync()
        {
            try { WishlistCount = await WishlistCountCache.GetAsync(); }
            catch { }
        }

        public void RefreshWishlistCount() =>
            WishlistCount = WishlistCountCache.Count;
    }
}