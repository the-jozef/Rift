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

                // 1. Získaj ID zoznam zo Steam (rýchlo — 1 request)
                var refs = await ApiService.GetWishlistIdsAsync(steamId);
                if (refs == null || refs.Count == 0)
                {
                    IsEmpty = true;
                    return;
                }

                // Zoraď: vydané od najnovšie pridaných → nevydané na konci
                // (nevieme ešte či sú vydané, tak zoradíme podľa DateAdded)
                refs = refs.OrderByDescending(r => r.DateAdded).ToList();

                TotalGames = refs.Count;
                IsLoading = false;

                // 2. Najprv zobraz čo máme v cache — okamžite
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

                // 3. Fetch chýbajúcich — batch (všetky naraz cez backend)
                if (toFetch.Count > 0)
                {
                    LoadingMessage = $"Fetching {toFetch.Count} new games...";

                    var fetchedGames = await ApiService.GetWishlistDetailedAsync(steamId);

                    foreach (var game in fetchedGames)
                    {
                        // Nastav dateAdded z ID listu
                        var refItem = toFetch.FirstOrDefault(r => r.AppId == game.AppId);
                        if (refItem != null) game.DateAddedUnix = refItem.DateAdded;

                        await WishlistGameCacheService.SaveAsync(game);
                        InsertSorted(game);
                    }
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
        }

        // ─── BACKGROUND SYNC ──────────────────────────────────────────────
        // Beží po načítaní — kontroluje len nové/odobrané hry

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
                        if (g != null) { Games.Remove(g); TotalGames = Games.Count; }
                    });
                    Debug.WriteLine($"[Wishlist] Removed: {removed}");
                }

                // Nové hry
                foreach (var r in freshRefs.Where(r => !currentIds.Contains(r.AppId)))
                {
                    var game = await ApiService.GetWishlistGameDetailAsync(r.AppId, r.DateAdded);
                    if (game == null) continue;
                    await WishlistGameCacheService.SaveAsync(game);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        InsertSorted(game);
                        TotalGames = Games.Count;
                    });
                    Debug.WriteLine($"[Wishlist] Added: {game.Name}");
                    await Task.Delay(1500);
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

        // ─── HELPER: vloží hru na správné miesto (vydané → nevydané na konci) ──

        private void InsertSorted(WishlistGameModel game)
        {
            // Nájdi správnu pozíciu:
            // vydané hry: zoradené od najnovšie pridaných (DateAddedUnix DESC)
            // nevydané: vždy na konci
            int index = 0;

            if (!game.IsReleased)
            {
                // Daj na koniec
                index = Games.Count;
            }
            else
            {
                // Nájdi prvú nevydanú hru alebo hru s menším DateAddedUnix
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