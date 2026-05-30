using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Languages;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Rift_App.ViewModels
{
    public partial class WishlistViewModel : ObservableObject
    {
        public ObservableCollection<WishlistGameModel> Games { get; } = new();

        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _isFetching = false;
        [ObservableProperty] private int _totalGames = 0;
        [ObservableProperty] private bool _isEmpty = false;

        [RelayCommand]
        private void SwitchToSk() => LanguageService.Switch("sk");

        [RelayCommand]
        private void SwitchToEn() => LanguageService.Switch("en");

        private CancellationTokenSource? _syncCts;

        private DateTime _lastVisibleRefresh = DateTime.MinValue;
        private const int VisibleRefreshMinutes = 30;

        public string WishlistTitle => string.IsNullOrEmpty(SessionManager.Username)
         ? L.Get("wishlist_title_my")
         : string.Format(L.Get("wishlist_title_user"),
             SessionManager.Username.ToUpper());

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

            try
            {
                var steamId = SessionManager.SteamId64;
                Debug.WriteLine($"[Wishlist] LoadWishlist for: {steamId}");

                if (string.IsNullOrEmpty(steamId))
                {
                    IsEmpty = true;
                    return;
                }

                var refs = await ApiService.GetWishlistIdsAsync(steamId);
                if (refs == null || refs.Count == 0)
                {
                    IsEmpty = true;
                    return;
                }

                refs = refs.OrderByDescending(r => r.DateAdded).ToList();
                TotalGames = refs.Count;

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

                IsLoading = false;

                if (toFetch.Count > 0)
                {
                    IsFetching = true;
                    await FetchMissingAsync(toFetch);
                    IsFetching = false;
                }

                IsEmpty = Games.Count == 0;

                _syncCts?.Cancel();
                _syncCts = new CancellationTokenSource();
                _ = SyncInBackgroundAsync(steamId, refs, _syncCts.Token);

                _lastVisibleRefresh = DateTime.UtcNow;
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
        public async Task RefreshIfStaleAsync()
        {
            if ((DateTime.UtcNow - _lastVisibleRefresh).TotalMinutes < VisibleRefreshMinutes)
                return;

            if (Games.Count == 0) return;

            await SyncOnceAsync();
            _lastVisibleRefresh = DateTime.UtcNow;
        }

        // ─── FETCH MISSING ────────────────────────────────────────────────

        private async Task FetchMissingAsync(List<WishlistItemRef> toFetch)
        {
            var dateMap = toFetch.ToDictionary(r => r.AppId, r => r.DateAdded);
            const int chunkSize = 10;
            var allFetched = new List<WishlistGameModel>();

            for (int i = 0; i < toFetch.Count; i += chunkSize)
            {
                var chunk = toFetch.Skip(i).Take(chunkSize).ToList();
                var appIds = chunk.Select(r => r.AppId).ToList();

                List<WishlistGameModel> fetched;
                try
                {
                    fetched = await ApiService.GetWishlistBatchAsync(appIds);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Wishlist] Batch error: {ex.Message}");
                    fetched = new();
                }

                var fetchedIds = new HashSet<int>(fetched.Select(g => g.AppId));
                var missingIds = appIds.Where(id => !fetchedIds.Contains(id)).ToList();

                foreach (var missingId in missingIds)
                {
                    Debug.WriteLine($"[Wishlist] Game {missingId} not found — removing");
                    WishlistGameCacheService.Delete(missingId);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var existing = Games.FirstOrDefault(g => g.AppId == missingId);
                        if (existing != null)
                        {
                            Games.Remove(existing);
                            TotalGames = Games.Count;
                        }
                    });
                }

                foreach (var game in fetched)
                {
                    if (dateMap.TryGetValue(game.AppId, out var dateAdded))
                        game.DateAddedUnix = dateAdded;
                    await WishlistGameCacheService.SaveAsync(game);
                    allFetched.Add(game);
                }
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                var all = Games.Concat(allFetched).ToList();
                RebuildSorted(all);
            });
        }

        // ─── SYNC ONCE — jednorázový sync pre RefreshIfStale ──────────────
        private async Task SyncOnceAsync()
        {
            try
            {
                var steamId = SessionManager.SteamId64;
                var freshRefs = await ApiService.GetWishlistIdsAsync(steamId);
                if (freshRefs == null) return;

                await ApplySyncDiff(steamId, freshRefs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] SyncOnce error: {ex.Message}");
            }
        }

        // ─── BACKGROUND SYNC ──────────────────────────────────────────────

        private async Task SyncInBackgroundAsync(
            string steamId,
            List<WishlistItemRef> currentRefs,
            CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), token);
                    if (token.IsCancellationRequested) break;

                    var freshRefs = await ApiService.GetWishlistIdsAsync(steamId);
                    if (freshRefs == null) continue;

                    await ApplySyncDiff(steamId, freshRefs);
                    currentRefs = freshRefs;

                    Debug.WriteLine("[Wishlist] Background sync done");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Wishlist] Sync error: {ex.Message}");
                }
            }
        }

        // ─── APPLY SYNC ──────────────────────────────────────────────────────────────────────

        private async Task ApplySyncDiff(string steamId, List<WishlistItemRef> freshRefs)
        {
            var currentIds = Games.Select(g => g.AppId).ToHashSet();
            var freshIds = freshRefs.Select(r => r.AppId).ToHashSet();

            var removedIds = currentIds.Except(freshIds).ToList();
            foreach (var removedId in removedIds)
            {
                WishlistGameCacheService.Delete(removedId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var g = Games.FirstOrDefault(x => x.AppId == removedId);
                    if (g != null)
                    {
                        Games.Remove(g);
                        TotalGames = Games.Count;
                        IsEmpty = Games.Count == 0;
                    }
                });
                Debug.WriteLine($"[Wishlist] Sync removed: {removedId}");
            }

            var newRefs = freshRefs.Where(r => !currentIds.Contains(r.AppId)).ToList();
            if (!newRefs.Any()) return;

            var dateMap = newRefs.ToDictionary(r => r.AppId, r => r.DateAdded);
            var appIds = newRefs.Select(r => r.AppId).ToList();

            List<WishlistGameModel> fetched;
            try
            {
                fetched = await ApiService.GetWishlistBatchAsync(appIds);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] Sync fetch error: {ex.Message}");
                return;
            }

            var fetchedIds = new HashSet<int>(fetched.Select(g => g.AppId));
            foreach (var missingId in appIds.Where(id => !fetchedIds.Contains(id)))
            {
                Debug.WriteLine($"[Wishlist] Sync: game {missingId} not found — skipping");
                WishlistGameCacheService.Delete(missingId);
            }

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

        // ─── SORTING ────────────────────────────────────────────────
        private void RebuildSorted(IEnumerable<WishlistGameModel> games)
        {
            var sorted = games
                .OrderBy(GetGroup)
                .ThenBy(GetPrice)
                .ToList();

            Games.Clear();
            foreach (var g in sorted)
                Games.Add(g);

            TotalGames = Games.Count;
            IsEmpty = Games.Count == 0;
        }

        private static int GetGroup(WishlistGameModel g)
        {
            if (!g.IsDlc && g.IsReleased && g.DiscountPercent > 0) return 0;
            if (g.IsDlc && g.IsReleased && g.DiscountPercent > 0) return 1;
            if (!g.IsDlc && !g.IsReleased && g.IsPreOrder) return 2;
            if (!g.IsDlc && g.IsReleased && g.DiscountPercent == 0) return 3;
            if (g.IsDlc && g.IsReleased && g.DiscountPercent == 0) return 4;
            return 5;
        }

        private static decimal GetPrice(WishlistGameModel g)
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

        private void InsertSorted(WishlistGameModel game)
        {
            int GetGroup(WishlistGameModel g)
            {
                if (!g.IsDlc && g.IsReleased && g.DiscountPercent > 0) return 0;
                if (g.IsDlc && g.IsReleased && g.DiscountPercent > 0) return 1;
                if (!g.IsDlc && !g.IsReleased && g.IsPreOrder) return 2;
                if (!g.IsDlc && g.IsReleased && g.DiscountPercent == 0) return 3;
                if (g.IsDlc && g.IsReleased && g.DiscountPercent == 0) return 4;
                return 5;
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