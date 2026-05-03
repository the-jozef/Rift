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
using System.Diagnostics;

namespace Rift_App.ViewModels
{
    public partial class StoreViewModel : ObservableObject
    {
        public ObservableCollection<GameModel> NewTrending { get; } = new();
        public ObservableCollection<GameModel> TopSellers { get; } = new();
        public ObservableCollection<GameModel> Specials { get; } = new();
        public ObservableCollection<GameModel> FeaturedGames { get; } = new();

        private int _newTrendingPage = 0, _topSellersPage = 0, _specialsPage = 0;
        private const int FeaturedTarget = 8;

        [ObservableProperty] private bool _isLoadingNewTrending = false;
        [ObservableProperty] private bool _isLoadingTopSellers = false;
        [ObservableProperty] private bool _isLoadingSpecials = false;
        [ObservableProperty] private bool _hasMoreNewTrending = true;
        [ObservableProperty] private bool _hasMoreTopSellers = true;
        [ObservableProperty] private bool _hasMoreSpecials = true;

        [ObservableProperty] private GameModel? _currentFeaturedGame;
        [ObservableProperty] private int _featuredIndex = 0;

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

        // ─── GENERICKÁ SEKCIA — CACHE FIRST ──────────────────────────────
        // FIX: ref parameter odstránený — používame Action<bool> namiesto ref
        // FIX: ref parameter removed — using Action<bool> instead of ref

        private async Task LoadSectionAsync(
            ObservableCollection<GameModel> collection,
            string cacheKey,
            TimeSpan ttl,
            Func<Task<List<GameModel>>> apiFetch,
            Action<int> setHasMore,
            Action<bool> setLoading)
        {
            setLoading(true);
            try
            {
                // 1. Skús cache — try cache
                var cached = await LocalCacheService.LoadAsync<List<GameModel>>(cacheKey, ttl);

                if (cached != null && cached.Count > 0)
                {
                    foreach (var g in cached) collection.Add(g);
                    setHasMore(cached.Count);
                    _ = PreloadListImagesAsync(cached);

                    // V pozadí skontroluj ceny — check prices in background
                    _ = CheckPricesAsync(collection, cacheKey, apiFetch);
                    return;
                }

                // 2. Cache prázdna — načítaj z API — cache empty — load from API
                var games = await apiFetch();
                if (games.Count > 0)
                {
                    foreach (var g in games) collection.Add(g);
                    setHasMore(games.Count);
                    await LocalCacheService.SaveAsync(cacheKey, games);
                    _ = PreloadListImagesAsync(games);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Store] LoadSection error: {ex.Message}");
            }
            finally
            {
                setLoading(false);
            }
        }

        // ─── NEW TRENDING ─────────────────────────────────────────────────

        private Task LoadNewTrendingAsync() => LoadSectionAsync(
            NewTrending,
            LocalCacheService.KeyTrending,
            LocalCacheService.StoreTTL,
            () => ApiService.GetNewTrendingAsync(_newTrendingPage),
            count => HasMoreNewTrending = count < 10,
            loading => IsLoadingNewTrending = loading);

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

        private Task LoadTopSellersAsync() => LoadSectionAsync(
            TopSellers,
            LocalCacheService.KeyTopSellers,
            LocalCacheService.StoreTTL,
            () => ApiService.GetTopSellersAsync(_topSellersPage),
            count => HasMoreTopSellers = count < 10,
            loading => IsLoadingTopSellers = loading);

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

        private Task LoadSpecialsAsync() => LoadSectionAsync(
            Specials,
            LocalCacheService.KeySpecials,
            LocalCacheService.StoreTTL,
            () => ApiService.GetSpecialsAsync(_specialsPage),
            count => HasMoreSpecials = count < 10,
            loading => IsLoadingSpecials = loading);

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

        // ─── KONTROLA CIEN V POZADÍ ───────────────────────────────────────
        // Background price check — waits 30s then compares prices with API

        private static async Task CheckPricesAsync(
            ObservableCollection<GameModel> collection,
            string cacheKey,
            Func<Task<List<GameModel>>> apiFetch)
        {
            try
            {
                await Task.Delay(30_000);
                var fresh = await apiFetch();
                if (fresh.Count == 0) return;

                bool changed = false;
                foreach (var freshGame in fresh)
                {
                    var existing = collection.FirstOrDefault(g => g.AppId == freshGame.AppId);
                    if (existing == null) continue;

                    if (existing.Price != freshGame.Price ||
                        existing.DiscountPercent != freshGame.DiscountPercent)
                    {
                        existing.Price = freshGame.Price;
                        existing.DiscountPercent = freshGame.DiscountPercent;
                        changed = true;
                        Debug.WriteLine($"[PriceCheck] {existing.Name}: {freshGame.Price}");
                    }
                }

                if (changed)
                    await LocalCacheService.SaveAsync(cacheKey, collection.ToList());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriceCheck] Error: {ex.Message}");
            }
        }

        // ─── FEATURED ─────────────────────────────────────────────────────

        private async Task LoadFeaturedAsync()
        {
            try
            {
                var cached = await LocalCacheService.LoadAsync<List<GameModel>>(
                    LocalCacheService.KeyFeatured, LocalCacheService.StoreTTL);

                if (cached != null && cached.Count >= 3)
                {
                    foreach (var g in cached) FeaturedGames.Add(g);
                    SetFeaturedGame(0);
                    _ = PreloadFeaturedImagesAsync(cached);
                    _ = RefreshFeaturedInBackgroundAsync();
                    return;
                }

                await FetchFeaturedFromApiAsync();

                if (FeaturedGames.Count > 0)
                    await LocalCacheService.SaveAsync(LocalCacheService.KeyFeatured, FeaturedGames.ToList());

                _ = PreloadFeaturedImagesAsync(FeaturedGames.ToList());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Featured] Error: {ex.Message}");
            }
        }

        // Obnoví featured v pozadí — refreshes featured in background after 30s
        private async Task RefreshFeaturedInBackgroundAsync()
        {
            await Task.Delay(30_000);
            try
            {
                var fresh = await ApiService.GetFeaturedAsync();
                if (fresh.Count == 0) return;

                var currentIds = FeaturedGames.Select(g => g.AppId).ToHashSet();
                var freshIds = fresh.Select(g => g.AppId).ToHashSet();
                if (currentIds.SetEquals(freshIds)) return;

                var libraryGenres = await GetLibraryGenresAsync();
                var fullGames = new List<GameModel>();

                using var semaphore = new SemaphoreSlim(2, 2);

                // FIX: Take(8) bez named parameter — Take(8) without named parameter
                foreach (var candidate in fresh.Take(8))
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var full = await ApiService.GetGameDetailsAsync(candidate.AppId);
                        if (full == null) continue;

                        bool recommended = full.Genres.Any(g =>
                            libraryGenres.Contains(g, StringComparer.OrdinalIgnoreCase));
                        full.StatusText = recommended ? "Recommended" : "Available Now";
                        fullGames.Add(full);
                    }
                    finally
                    {
                        semaphore.Release();
                        await Task.Delay(2000);
                    }
                }

                if (fullGames.Count >= 3)
                    await LocalCacheService.SaveAsync(LocalCacheService.KeyFeatured, fullGames);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Featured] Background refresh error: {ex.Message}");
            }
        }

        private async Task FetchFeaturedFromApiAsync()
        {
            var libraryGenres = await GetLibraryGenresAsync();

            List<GameModel> candidates = await ApiService.GetFeaturedAsync();
            if (candidates.Count == 0)
                candidates = await ApiService.GetNewTrendingAsync(0);

            int page = 0;
            int retries = 0;

            while (FeaturedGames.Count < FeaturedTarget && retries < 5)
            {
                retries++;
                using var semaphore = new SemaphoreSlim(2, 2);

                foreach (var candidate in candidates)
                {
                    if (FeaturedGames.Count >= FeaturedTarget) break;
                    if (FeaturedGames.Any(g => g.AppId == candidate.AppId)) continue;

                    await semaphore.WaitAsync();
                    try
                    {
                        var full = await ApiService.GetGameDetailsAsync(candidate.AppId);
                        if (full == null) { await Task.Delay(2000); continue; }

                        bool recommended = full.Genres.Any(g =>
                            libraryGenres.Contains(g, StringComparer.OrdinalIgnoreCase));
                        full.StatusText = recommended ? "Recommended" : "Available Now";

                        FeaturedGames.Add(full);

                        if (FeaturedGames.Count == 1)
                        {
                            SetFeaturedGame(0);
                            _ = PreloadSingleGameImagesAsync(full);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                        await Task.Delay(2000);
                    }
                }

                if (FeaturedGames.Count < FeaturedTarget)
                {
                    await Task.Delay(5000);
                    page++;
                    candidates = await ApiService.GetNewTrendingAsync(page);
                }
            }
        }

        // ─── IMAGE PRELOADING ─────────────────────────────────────────────

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
            if (!string.IsNullOrEmpty(game.HeaderImageUrl))
                await ImageCacheService.GetAsync(game.HeaderImageUrl);

            foreach (var shot in game.Screenshots.Take(4))
            {
                await ImageCacheService.GetAsync(shot);
                await Task.Delay(150);
            }
        }

        private static async Task PreloadListImagesAsync(List<GameModel> games)
        {
            using var semaphore = new SemaphoreSlim(3, 3);
            var tasks = games.Select(async g =>
            {
                if (string.IsNullOrEmpty(g.HeaderImageUrl)) return;
                await semaphore.WaitAsync();
                try { await ImageCacheService.GetAsync(g.HeaderImageUrl); }
                finally { semaphore.Release(); await Task.Delay(100); }
            });
            await Task.WhenAll(tasks);
        }

        // ─── SET FEATURED GAME ────────────────────────────────────────────

        private void SetFeaturedGame(int index)
        {
            FeaturedIndex = index;
            CurrentFeaturedGame = FeaturedGames[index];

            var game = FeaturedGames[index];
            FeaturedMainImage = new GameImageViewModel(game.HeaderImageUrl);
            FeaturedScreenshot1 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(0) ?? "");
            FeaturedScreenshot2 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(1) ?? "");
            FeaturedScreenshot3 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(2) ?? "");
            FeaturedScreenshot4 = new GameImageViewModel(game.Screenshots.ElementAtOrDefault(3) ?? "");
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