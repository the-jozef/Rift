using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace Rift_App.ViewModels
{
    public partial class WishlistViewModel : ObservableObject
    {
        public ObservableCollection<WishlistGameModel> Games { get; } = new();

        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _isFetching = false;
        [ObservableProperty] private int _totalGames = 0;
        [ObservableProperty] private bool _isEmpty = false;
        [ObservableProperty] private string _loadingMessage = "Loading wishlist...";
        private CancellationTokenSource? _syncCts;

        public string WishlistTitle =>
            string.IsNullOrEmpty(SessionManager.Username)
                ? "MY WISHLIST"
                : $"{SessionManager.Username.ToUpper()}'S WISHLIST";

        public event Action<GameModel>? OnGameSelected;
        public string AvatarUrl => SessionManager.AvatarUrl;

        // ─── LOAD ─────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadWishlistAsync()
        {
            IsLoading = true;
            IsFetching = false;
            Games.Clear();
            IsEmpty = false;
            LoadingMessage = "Loading wishlist...";

            try
            {
                var steamId = SessionManager.SteamId64;
                Debug.WriteLine($"[Wishlist] LoadWishlist for: {steamId}");

                if (string.IsNullOrEmpty(steamId))
                {
                    Debug.WriteLine("[Wishlist] No SteamId — abort");
                    IsEmpty = true;
                    return;
                }

                // 1. Get Id list
                var refs = await ApiService.GetWishlistIdsAsync(steamId);
                if (refs == null || refs.Count == 0)
                {
                    Debug.WriteLine("[Wishlist] No wishlist items");
                    IsEmpty = true;
                    return;
                }

                refs = refs.OrderByDescending(r => r.DateAdded).ToList();
                TotalGames = refs.Count;
                Debug.WriteLine($"[Wishlist] Total items: {refs.Count}");

                // 2. Show cached games IMMEDIATELY
                var toFetch = new List<WishlistItemRef>();
                foreach (var r in refs)
                {
                    var cached = await WishlistGameCacheService.LoadAsync(r.AppId);
                    if (cached != null)
                    {
                        cached.DateAddedUnix = r.DateAdded;
                        InsertSorted(cached);
                    }
                    else
                    {
                        toFetch.Add(r);
                    }
                }

                Debug.WriteLine($"[Wishlist] From cache: {Games.Count}, to fetch: {toFetch.Count}");

                // 3. IsLoading = false, so UI can display and the user can see at least something while missing data is being fetched.
                IsLoading = false;

                // 4. Fetch
                if (toFetch.Count > 0)
                {
                    IsFetching = true;
                    await FetchMissingAsync(toFetch);
                    IsFetching = false;
                }

                IsEmpty = Games.Count == 0;
                Debug.WriteLine($"[Wishlist] Final count: {Games.Count}");

                _syncCts?.Cancel();
                _syncCts = new CancellationTokenSource();

                // 5. Sync in background every 15 minutes to keep wishlist up-to-date
                _ = SyncInBackgroundAsync(steamId, refs, _syncCts.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] Load error: {ex.Message}");
                IsEmpty = Games.Count == 0;
                IsLoading = false;
                IsFetching = false;
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (Games.Count == 0) return;

            var steamId = SessionManager.SteamId64;
            var allIds = Games.Select(g => new WishlistItemRef
            {
                AppId = g.AppId,
                DateAdded = g.DateAddedUnix
            }).ToList();

            foreach (var game in Games)
                WishlistGameCacheService.Delete(game.AppId);

            IsFetching = true;
            LoadingMessage = "Refreshing...";
            await FetchMissingAsync(allIds);
            IsFetching = false;
            LoadingMessage = string.Empty;
        }

        // ─── FETCH ────────────────────────────────────────────

        private async Task FetchMissingAsync(List<WishlistItemRef> toFetch)
        {
            var dateMap = toFetch.ToDictionary(r => r.AppId, r => r.DateAdded);
            const int chunkSize = 10;
            int total = toFetch.Count;

            for (int i = 0; i < toFetch.Count; i += chunkSize)
            {
                var chunk = toFetch.Skip(i).Take(chunkSize).ToList();
                var appIds = chunk.Select(r => r.AppId).ToList();

                int remaining = total - i;
                LoadingMessage = $"Fetching {remaining} games...";
                Debug.WriteLine($"[Wishlist] Fetching chunk: {string.Join(",", appIds)}");

                List<WishlistGameModel> fetched;
                try
                {
                    fetched = await ApiService.GetWishlistBatchAsync(appIds);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Wishlist] Batch error: {ex.Message}");
                    fetched = new List<WishlistGameModel>();
                }

                Debug.WriteLine($"[Wishlist] Chunk returned: {fetched.Count} games");

                foreach (var game in fetched)
                {
                    if (dateMap.TryGetValue(game.AppId, out var dateAdded))
                        game.DateAddedUnix = dateAdded;

                    await WishlistGameCacheService.SaveAsync(game);
                    InsertSorted(game);
                }
            }

            LoadingMessage = string.Empty;
        }

        // ─── BACKGROUND SYNC ──────────────────────────────────────────────

        private async Task SyncInBackgroundAsync(string steamId, List<WishlistItemRef> currentRefs, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), token);
                    if (token.IsCancellationRequested) break;

                    Debug.WriteLine("[Wishlist] Sync started");

                    var freshRefs = await ApiService.GetWishlistIdsAsync(steamId);
                    if (freshRefs == null) continue;

                    var currentIds = currentRefs.Select(r => r.AppId).ToHashSet();
                    var freshIds = freshRefs.Select(r => r.AppId).ToHashSet();

                    // Delete from wishlist
                    foreach (var removed in currentIds.Except(freshIds))
                    {
                        WishlistGameCacheService.Delete(removed);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var g = Games.FirstOrDefault(x => x.AppId == removed);
                            if (g != null) { Games.Remove(g); TotalGames = Games.Count; }
                        });
                        Debug.WriteLine($"[Wishlist] Sync removed: {removed}");
                    }

                    // New in wishlist
                    var newRefs = freshRefs.Where(r => !currentIds.Contains(r.AppId)).ToList();
                    if (newRefs.Any())
                    {
                        var dateMap = newRefs.ToDictionary(r => r.AppId, r => r.DateAdded);
                        var fetched = await ApiService.GetWishlistBatchAsync(
                            newRefs.Select(r => r.AppId).ToList());

                        foreach (var game in fetched)
                        {
                            if (dateMap.TryGetValue(game.AppId, out var da))
                                game.DateAddedUnix = da;

                            await WishlistGameCacheService.SaveAsync(game);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                InsertSorted(game);
                                TotalGames = Games.Count;
                                IsEmpty = false;
                            });
                            Debug.WriteLine($"[Wishlist] Sync added: {game.Name}");
                        }
                    }

                    // Update currentRefs for next cycle
                    currentRefs = freshRefs;

                    Debug.WriteLine("[Wishlist] Sync done");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Wishlist] Sync error: {ex.Message}");
                }
            }
        }

        // ─── COMMANDS ─────────────────────────────────────────────────────

        [RelayCommand]
        public async Task RemoveGameAsync(WishlistGameModel game)
        {
            if (game == null) return;
            Games.Remove(game);
            WishlistCountCache.Set(Games.Count);
            TotalGames = Games.Count;
            IsEmpty = Games.Count == 0;
            WishlistGameCacheService.Delete(game.AppId);
            await ApiService.RemoveFromWishlistAsync(SessionManager.SteamId64, game.AppId);

            // Open Steam wishlist page for this game 
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"steam://store/{game.AppId}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] RemoveGame open steam error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SelectGame(WishlistGameModel game)
        {
            if (game == null) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"steam://store/{game.AppId}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] SelectGame error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddToCart(WishlistGameModel game)
        {
            if (game == null) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"steam://store/{game.AppId}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] AddToCart error: {ex.Message}");
            }
        }

        // ─── INSERT SORTED ────────────────────────────────────────────────

        private void InsertSorted(WishlistGameModel game)
        {
            int GetGroup(WishlistGameModel g)
            {
                if (!g.IsDlc && g.IsReleased && g.DiscountPercent > 0) return 0; // released game on sale
                if (g.IsDlc && g.IsReleased && g.DiscountPercent > 0) return 1; // released DLC on sale
                if (!g.IsDlc && !g.IsReleased && g.IsPreOrder) return 2; // pre-order game
                if (!g.IsDlc && g.IsReleased && g.DiscountPercent == 0) return 3; // released game
                if (g.IsDlc && g.IsReleased && g.DiscountPercent == 0) return 4; // released DLC
                return 5;                                                           // unreleased
            }

            decimal GetPrice(WishlistGameModel g)
            {
                if (g.IsFree || g.Price is "Free" or "N/A") return 0;
                var cleaned = g.Price
                    .Replace("$", "").Replace("€", "")
                    .Replace(" ", "")
                    .Replace(",", ".").Trim();
                return decimal.TryParse(cleaned,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var val) ? val : 0;
            }

            int gameGroup = GetGroup(game);
            decimal gamePrice = GetPrice(game);
            int index = Games.Count;

            for (int i = 0; i < Games.Count; i++)
            {
                int existingGroup = GetGroup(Games[i]);
                decimal existingPrice = GetPrice(Games[i]);

                if (existingGroup > gameGroup) { index = i; break; }
                if (existingGroup == gameGroup && existingPrice < gamePrice) { index = i; break; }
            }

            Games.Insert(index, game);
            TotalGames = Games.Count;
            IsEmpty = false;
        }
    }
}