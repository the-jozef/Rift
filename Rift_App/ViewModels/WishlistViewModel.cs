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
        [ObservableProperty] private int _totalGames = 0;
        [ObservableProperty] private bool _isEmpty = false;
        [ObservableProperty] private string _loadingMessage = "Loading wishlist...";

        public string WishlistTitle =>
            string.IsNullOrEmpty(SessionManager.Username)
                ? "MY WISHLIST"
                : $"{SessionManager.Username.ToUpper()}'S WISHLIST";

        public event Action<GameModel>? OnGameSelected;

        // ─── LOAD ─────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadWishlistAsync()
        {
            IsLoading = true;
            Games.Clear();
            IsEmpty = false;
            LoadingMessage = "Loading wishlist...";

            try
            {
                var steamId = SessionManager.SteamId64;

                // 1. Získaj ID zoznam — rýchle (1 API call)
                var refs = await ApiService.GetWishlistIdsAsync(steamId);
                if (refs == null || refs.Count == 0)
                {
                    IsEmpty = true;
                    return;
                }

                refs = refs.OrderByDescending(r => r.DateAdded).ToList();
                TotalGames = refs.Count;

                // 2. Zobraz cached hry ihneď — z AppData\wishlist\games\
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

                // 3. Fetch chýbajúce hry v batchoch po 10
                //    Zobrazujú sa priebežne po každom batchi
                if (toFetch.Count > 0)
                {
                    await FetchMissingInChunksAsync(toFetch);
                }

                IsEmpty = Games.Count == 0;

                // 4. Sync v pozadí — skontroluj pridané/odobrané hry
                _ = SyncInBackgroundAsync(steamId, refs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] Load error: {ex.Message}");
                IsEmpty = Games.Count == 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ─── FETCH MISSING — po 10 hrách, zobrazuje priebežne ────────────

        private async Task FetchMissingInChunksAsync(List<WishlistItemRef> toFetch)
        {
            var dateMap = toFetch.ToDictionary(r => r.AppId, r => r.DateAdded);
            const int chunkSize = 10;

            for (int i = 0; i < toFetch.Count; i += chunkSize)
            {
                var chunk = toFetch.Skip(i).Take(chunkSize).ToList();
                var appIds = chunk.Select(r => r.AppId).ToList();

                int remaining = toFetch.Count - i;
                LoadingMessage = $"Fetching {remaining} games...";

                var fetched = await ApiService.GetWishlistBatchAsync(appIds);

                foreach (var game in fetched)
                {
                    if (dateMap.TryGetValue(game.AppId, out var dateAdded))
                        game.DateAddedUnix = dateAdded;

                    await WishlistGameCacheService.SaveAsync(game);
                    InsertSorted(game);
                }

                Debug.WriteLine($"[Wishlist] Fetched chunk {i / chunkSize + 1}: {fetched.Count} games");
            }
        }

        // ─── BACKGROUND SYNC ──────────────────────────────────────────────

        private async Task SyncInBackgroundAsync(string steamId, List<WishlistItemRef> currentRefs)
        {
            try
            {
                await Task.Delay(5000);

                var freshRefs = await ApiService.GetWishlistIdsAsync(steamId);
                if (freshRefs == null) return;

                var currentIds = currentRefs.Select(r => r.AppId).ToHashSet();
                var freshIds = freshRefs.Select(r => r.AppId).ToHashSet();

                // Odobrané hry
                foreach (var removed in currentIds.Except(freshIds))
                {
                    WishlistGameCacheService.Delete(removed);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var g = Games.FirstOrDefault(x => x.AppId == removed);
                        if (g != null)
                        {
                            Games.Remove(g);
                            TotalGames = Games.Count;
                        }
                    });
                    Debug.WriteLine($"[Wishlist] Removed: {removed}");
                }

                // Nové hry
                var newRefs = freshRefs.Where(r => !currentIds.Contains(r.AppId)).ToList();
                if (newRefs.Any())
                {
                    var dateMap = newRefs.ToDictionary(r => r.AppId, r => r.DateAdded);
                    var newAppIds = newRefs.Select(r => r.AppId).ToList();
                    var fetched = await ApiService.GetWishlistBatchAsync(newAppIds);

                    foreach (var game in fetched)
                    {
                        if (dateMap.TryGetValue(game.AppId, out var dateAdded))
                            game.DateAddedUnix = dateAdded;

                        await WishlistGameCacheService.SaveAsync(game);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            InsertSorted(game);
                            TotalGames = Games.Count;
                            IsEmpty = false;
                        });

                        Debug.WriteLine($"[Wishlist] Added: {game.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] Sync error: {ex.Message}");
            }
        }

        // ─── REMOVE ───────────────────────────────────────────────────────

        [RelayCommand]
        public async Task RemoveGameAsync(WishlistGameModel game)
        {
            if (game == null) return;

            Games.Remove(game);
            TotalGames = Games.Count;
            IsEmpty = Games.Count == 0;

            WishlistGameCacheService.Delete(game.AppId);
            await ApiService.RemoveFromWishlistAsync(SessionManager.SteamId64, game.AppId);
        }

        // ─── SELECT ───────────────────────────────────────────────────────

        [RelayCommand]
        private void SelectGame(WishlistGameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game.ToGameModel());
        }

        // ─── ADD TO CART ──────────────────────────────────────────────────

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
        // Vydané hry: zoradené od najnovšie pridaných (DESC)
        // Nevydané: vždy na konci

        private void InsertSorted(WishlistGameModel game)
        {
            int index;

            if (!game.IsReleased)
            {
                index = Games.Count;
            }
            else
            {
                index = Games.Count;
                for (int i = 0; i < Games.Count; i++)
                {
                    if (!Games[i].IsReleased || Games[i].DateAddedUnix < game.DateAddedUnix)
                    {
                        index = i;
                        break;
                    }
                }
            }

            Games.Insert(index, game);
            TotalGames = Games.Count;
        }
    }
}