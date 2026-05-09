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

        public string Username => SessionManager.Username;

        public event Action<GameModel>? OnGameSelected;

        // ─── LOAD ─────────────────────────────────────────────────────────
        // 1. Zobraz cache okamžite
        // 2. Sync v pozadí (pridané / odobrané / ceny)

        [RelayCommand]
        public async Task LoadWishlistAsync()
        {
            IsLoading = true;
            Games.Clear();
            IsEmpty = false;

            try
            {
                var steamId = SessionManager.SteamId64;

                // 1. Cache — okamžité zobrazenie
                var cached = await WishlistCacheService.LoadAsync(steamId);

                if (cached != null && cached.Count > 0)
                {
                    Populate(cached);
                    IsLoading = false;

                    // 2. Sync v pozadí
                    _ = SyncInBackgroundAsync(steamId, cached);
                    return;
                }

                // 3. Prvé spustenie — načítaj zo Steam
                var fresh = await ApiService.GetWishlistDetailedAsync(steamId);

                if (fresh == null || fresh.Count == 0)
                {
                    IsEmpty = true;
                    TotalGames = 0;
                    return;
                }

                Populate(fresh);
                await WishlistCacheService.SaveAsync(steamId, fresh);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] Load error: {ex.Message}");
                IsEmpty = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ─── BACKGROUND SYNC ──────────────────────────────────────────────

        private async Task SyncInBackgroundAsync(string steamId, List<WishlistGameModel> cached)
        {
            try
            {
                await Task.Delay(2000);

                var fresh = await ApiService.GetWishlistDetailedAsync(steamId);
                if (fresh == null || fresh.Count == 0) return;

                var (synced, changed) = await WishlistCacheService.SyncAsync(cached, fresh);
                if (!changed) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Games.Clear();
                    Populate(synced);
                });

                await WishlistCacheService.SaveAsync(steamId, synced);
                Debug.WriteLine("[Wishlist] Sync complete.");
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

            // Okamžite odober z UI
            Games.Remove(game);
            TotalGames = Games.Count;
            IsEmpty = Games.Count == 0;

            try
            {
                // Odober z local cache
                await WishlistCacheService.RemoveAsync(SessionManager.SteamId64, game.AppId);

                // Pokus o remove zo Steam (môže zlyhať pre private profily — ignorujeme)
                await ApiService.RemoveFromWishlistAsync(SessionManager.SteamId64, game.AppId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] Remove error: {ex.Message}");
            }
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

        // ─── HELPER ───────────────────────────────────────────────────────

        private void Populate(List<WishlistGameModel> games)
        {
            // Vydané: od najnovšie pridaných → nevydané na konci
            var sorted = games
                .OrderBy(g => !g.IsReleased)
                .ThenByDescending(g => g.DateAddedUnix)
                .ToList();

            foreach (var g in sorted) Games.Add(g);
            TotalGames = Games.Count;
            IsEmpty = Games.Count == 0;
        }
    }
}