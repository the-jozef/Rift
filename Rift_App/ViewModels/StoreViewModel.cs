using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Models;
using Rift_App.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Rift_App.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Rift_App.ViewModels
{
    public partial class StoreViewModel : ObservableObject
    {
        public ObservableCollection<GameModel> NewTrending { get; } = new();
        public ObservableCollection<GameModel> TopSellers { get; } = new();
        public ObservableCollection<GameModel> Specials { get; } = new();
        public ObservableCollection<GameModel> FeaturedGames { get; } = new();

        private int _newTrendingPage = 0, _topSellersPage = 0, _specialsPage = 0;

        [ObservableProperty] private bool _isLoadingNewTrending = false;
        [ObservableProperty] private bool _isLoadingTopSellers = false;
        [ObservableProperty] private bool _isLoadingSpecials = false;
        [ObservableProperty] private bool _hasMoreNewTrending = true;
        [ObservableProperty] private bool _hasMoreTopSellers = true;
        [ObservableProperty] private bool _hasMoreSpecials = true;

        [ObservableProperty] private GameModel? _currentFeaturedGame;
        [ObservableProperty] private int _featuredIndex = 0;

        // GameImageViewModel pre featured — spravuje async načítanie obrázkov
        // GameImageViewModel for featured — manages async image loading
        [ObservableProperty] private GameImageViewModel? _featuredMainImage;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot1;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot2;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot3;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot4;

        public event Action<GameModel>? OnGameSelected;

        // ─── LOAD ALL ─────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadStoreAsync()
        {
            // Inicializuj tag slovník — initialize tag dictionary
            await TagService.InitAsync();

            await LoadFeaturedAsync();
            await Task.Delay(2000);
            await LoadNewTrendingAsync();
            await Task.Delay(2000);
            await LoadTopSellersAsync();
            await Task.Delay(2000);
            await LoadSpecialsAsync();
            await ApiService.SaveSessionAsync("Store");
        }

        // ─── FEATURED ─────────────────────────────────────────────────────

        private async Task LoadFeaturedAsync()
        {
            const int target = 8;
            const int maxRetries = 5;

            // Skús local cache — try local cache
            var cached = await LocalCacheService.LoadAsync<List<GameModel>>(
                LocalCacheService.KeyFeatured, LocalCacheService.StoreTTL);

            if (cached != null && cached.Count >= 3)
            {
                foreach (var g in cached) FeaturedGames.Add(g);
                SetFeaturedGame(0);

                // Preload obrázkov z cache — preload images from cache
                _ = PreloadFeaturedImagesAsync(cached);
                return;
            }

            // Nie je cache — stiahni — no cache — download
            var libraryGenres = await GetLibraryGenresAsync();

            // Featured endpoint (specials sekcia) — featured endpoint (specials section)
            List<GameModel> candidates = await ApiService.GetFeaturedAsync();
            if (candidates.Count == 0)
                candidates = await ApiService.GetNewTrendingAsync(0);

            int page = 0;
            int retries = 0;

            while (FeaturedGames.Count < target && retries < maxRetries)
            {
                retries++;

                // Max 3 súčasné requesty — max 3 concurrent requests
                using var semaphore = new SemaphoreSlim(3, 3);

                foreach (var candidate in candidates)
                {
                    if (FeaturedGames.Count >= target) break;
                    if (FeaturedGames.Any(g => g.AppId == candidate.AppId)) continue;

                    await semaphore.WaitAsync();
                    try
                    {
                        var full = await ApiService.GetGameDetailsAsync(candidate.AppId);
                        if (full == null) { await Task.Delay(1000); continue; }

                        bool recommended = full.Genres.Any(g =>
                            libraryGenres.Contains(g, StringComparer.OrdinalIgnoreCase));
                        full.StatusText = recommended ? "Recommended" : "Available Now";

                        FeaturedGames.Add(full);

                        if (FeaturedGames.Count == 1)
                        {
                            SetFeaturedGame(0);
                            // Začni načítavať obrázok okamžite — start loading image immediately
                            _ = PreloadSingleGameImagesAsync(full);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                        await Task.Delay(1500);
                    }
                }

                if (FeaturedGames.Count < target)
                {
                    await Task.Delay(5000);
                    page++;
                    candidates = await ApiService.GetNewTrendingAsync(page);
                }
            }

            // Ulož do cache — save to cache
            if (FeaturedGames.Count > 0)
                await LocalCacheService.SaveAsync(LocalCacheService.KeyFeatured, FeaturedGames.ToList());

            // Preload všetkých obrázkov — preload all images
            _ = PreloadFeaturedImagesAsync(FeaturedGames.ToList());
        }

        // ─── IMAGE PRELOADING ─────────────────────────────────────────────

        // Preloaduje header + 4 screenshoty pre každú featured hru
        // Preloads header + 4 screenshots for each featured game
        private async Task PreloadFeaturedImagesAsync(List<GameModel> games)
        {
            foreach (var game in games)
            {
                await PreloadSingleGameImagesAsync(game);
                await Task.Delay(200);
            }
        }

        private static async Task PreloadSingleGameImagesAsync(GameModel game)
        {
            // 1 veľký header — 1 large header
            if (!string.IsNullOrEmpty(game.HeaderImageUrl))
                await ImageCacheService.GetAsync(game.HeaderImageUrl);

            // 4 malé screenshoty — 4 small screenshots
            var shots = game.Screenshots.Take(4).ToList();
            foreach (var shot in shots)
            {
                await ImageCacheService.GetAsync(shot);
                await Task.Delay(150);
            }
        }

        // ─── SET FEATURED GAME ────────────────────────────────────────────

        private void SetFeaturedGame(int index)
        {
            FeaturedIndex = index;
            CurrentFeaturedGame = FeaturedGames[index];

            var game = FeaturedGames[index];

            // Vytvor GameImageViewModel pre každý obrázok
            // Create GameImageViewModel for each image
            FeaturedMainImage = new GameImageViewModel(game.HeaderImageUrl);
            FeaturedScreenshot1 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(0) ?? "");
            FeaturedScreenshot2 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(1) ?? "");
            FeaturedScreenshot3 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(2) ?? "");
            FeaturedScreenshot4 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(3) ?? "");
        }

        // ─── NEW TRENDING ─────────────────────────────────────────────────

        private async Task LoadNewTrendingAsync()
        {
            IsLoadingNewTrending = true;
            try
            {
                // Skús cache — try cache
                var cached = await LocalCacheService.LoadAsync<List<GameModel>>(
                    LocalCacheService.KeyTrending, LocalCacheService.StoreTTL);

                List<GameModel> games;
                if (cached != null && cached.Count > 0)
                {
                    games = cached;
                }
                else
                {
                    games = await ApiService.GetNewTrendingAsync(_newTrendingPage);
                    if (games.Count > 0)
                        await LocalCacheService.SaveAsync(LocalCacheService.KeyTrending, games);
                }

                foreach (var g in games) NewTrending.Add(g);
                if (games.Count < 10) HasMoreNewTrending = false;

                // Preload header obrázkov — preload header images
                _ = PreloadListImagesAsync(games);
            }
            catch { }
            finally { IsLoadingNewTrending = false; }
        }

        [RelayCommand]
        private async Task ShowMoreNewTrendingAsync()
        {
            _newTrendingPage++;
            IsLoadingNewTrending = true;
            try
            {
                var games = await ApiService.GetNewTrendingAsync(_newTrendingPage);
                foreach (var g in games) NewTrending.Add(g);
                if (games.Count < 10) HasMoreNewTrending = false;
                _ = PreloadListImagesAsync(games);
            }
            catch { }
            finally { IsLoadingNewTrending = false; }
        }

        // ─── TOP SELLERS ──────────────────────────────────────────────────

        private async Task LoadTopSellersAsync()
        {
            IsLoadingTopSellers = true;
            try
            {
                var cached = await LocalCacheService.LoadAsync<List<GameModel>>(
                    LocalCacheService.KeyTopSellers, LocalCacheService.StoreTTL);

                List<GameModel> games;
                if (cached != null && cached.Count > 0)
                {
                    games = cached;
                }
                else
                {
                    games = await ApiService.GetTopSellersAsync(_topSellersPage);
                    if (games.Count > 0)
                        await LocalCacheService.SaveAsync(LocalCacheService.KeyTopSellers, games);
                }

                foreach (var g in games) TopSellers.Add(g);
                if (games.Count < 10) HasMoreTopSellers = false;
                _ = PreloadListImagesAsync(games);
            }
            catch { }
            finally { IsLoadingTopSellers = false; }
        }

        [RelayCommand]
        private async Task ShowMoreTopSellersAsync()
        {
            _topSellersPage++;
            IsLoadingTopSellers = true;
            try
            {
                var games = await ApiService.GetTopSellersAsync(_topSellersPage);
                foreach (var g in games) TopSellers.Add(g);
                if (games.Count < 10) HasMoreTopSellers = false;
                _ = PreloadListImagesAsync(games);
            }
            catch { }
            finally { IsLoadingTopSellers = false; }
        }

        // ─── SPECIALS ─────────────────────────────────────────────────────

        private async Task LoadSpecialsAsync()
        {
            IsLoadingSpecials = true;
            try
            {
                var cached = await LocalCacheService.LoadAsync<List<GameModel>>(
                    LocalCacheService.KeySpecials, LocalCacheService.StoreTTL);

                List<GameModel> games;
                if (cached != null && cached.Count > 0)
                {
                    games = cached;
                }
                else
                {
                    games = await ApiService.GetSpecialsAsync(_specialsPage);
                    if (games.Count > 0)
                        await LocalCacheService.SaveAsync(LocalCacheService.KeySpecials, games);
                }

                foreach (var g in games) Specials.Add(g);
                if (games.Count < 10) HasMoreSpecials = false;
                _ = PreloadListImagesAsync(games);
            }
            catch { }
            finally { IsLoadingSpecials = false; }
        }

        [RelayCommand]
        private async Task ShowMoreSpecialsAsync()
        {
            _specialsPage++;
            IsLoadingSpecials = true;
            try
            {
                var games = await ApiService.GetSpecialsAsync(_specialsPage);
                foreach (var g in games) Specials.Add(g);
                if (games.Count < 10) HasMoreSpecials = false;
                _ = PreloadListImagesAsync(games);
            }
            catch { }
            finally { IsLoadingSpecials = false; }
        }

        // ─── IMAGE PRELOAD PRE LISTY ──────────────────────────────────────
        // Image preload for lists — only header images (not screenshots)
        private static async Task PreloadListImagesAsync(List<GameModel> games)
        {
            using var semaphore = new SemaphoreSlim(3, 3);
            var tasks = games.Select(async g =>
            {
                await semaphore.WaitAsync();
                try { await ImageCacheService.GetAsync(g.HeaderImageUrl); }
                finally
                {
                    semaphore.Release();
                    await Task.Delay(100);
                }
            });
            await Task.WhenAll(tasks);
        }

        // ─── HELPERS ─────────────────────────────────────────────────────

        private async Task<HashSet<string>> GetLibraryGenresAsync()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!SessionManager.IsLoggedIn) return result;
                var library = await ApiService.GetLibraryAsync(SessionManager.SteamId64);
                foreach (var game in library)
                    foreach (var genre in game.Genres)
                        result.Add(genre);
            }
            catch { }
            return result;
        }

        // ─── CAROUSEL ─────────────────────────────────────────────────────

        [RelayCommand]
        private void NextFeatured()
        {
            if (FeaturedGames.Count == 0) return;
            SetFeaturedGame((FeaturedIndex + 1) % FeaturedGames.Count);
        }

        [RelayCommand]
        private void PrevFeatured()
        {
            if (FeaturedGames.Count == 0) return;
            SetFeaturedGame((FeaturedIndex - 1 + FeaturedGames.Count) % FeaturedGames.Count);
        }

        [RelayCommand]
        private void SelectFeatured(string indexStr)
        {
            if (int.TryParse(indexStr, out int index) && index < FeaturedGames.Count)
                SetFeaturedGame(index);
        }

        // ─── SELECT GAME ──────────────────────────────────────────────────

        [RelayCommand]
        private void SelectGame(GameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game);
        }
    }
}