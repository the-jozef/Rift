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
        // ─── COLLECTIONS ──────────────────────────────────────────────────
        public ObservableCollection<GameModel> NewTrending { get; } = new();
        public ObservableCollection<GameModel> TopSellers { get; } = new();
        public ObservableCollection<GameModel> Specials { get; } = new();
        public ObservableCollection<GameModel> FeaturedGames { get; } = new();

        // ─── CONFIG ───────────────────────────────────────────────────────
        private const int FeaturedTarget = 8;   // hry vo featured / games in featured
        private const int PageSize = 8;   // show more krok / show more step

        // ─── PAGE COUNTERS ────────────────────────────────────────────────
        private int _newTrendingPage = 0;
        private int _topSellersPage = 0;
        private int _specialsPage = 0;

        // ─── OBSERVABLE PROPERTIES ────────────────────────────────────────
        [ObservableProperty] private bool _isLoadingNewTrending;
        [ObservableProperty] private bool _isLoadingTopSellers;
        [ObservableProperty] private bool _isLoadingSpecials;
        [ObservableProperty] private bool _hasMoreNewTrending = true;
        [ObservableProperty] private bool _hasMoreTopSellers = true;
        [ObservableProperty] private bool _hasMoreSpecials = true;

        [ObservableProperty] private GameModel? _currentFeaturedGame;
        [ObservableProperty] private int _featuredIndex;
        [ObservableProperty] private GameImageViewModel? _featuredMainImage;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot1;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot2;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot3;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot4;

        public event Action<GameModel>? OnGameSelected;

        // ─────────────────────────────────────────────────────────────────
        //  LOAD ALL
        // ─────────────────────────────────────────────────────────────────
        [RelayCommand]
        public async Task LoadStoreAsync()
        {
            await TagService.InitAsync();

            // Featured má prioritu — zobrazí sa čo najskôr
            // Featured has priority — shown as fast as possible
            await LoadFeaturedAsync();

            // Ostatné sekcie s malým oneskorením aby Featured nezabralo celý bandwidth
            await Task.Delay(800);
            await LoadNewTrendingAsync();
            await Task.Delay(800);
            await LoadTopSellersAsync();
            await Task.Delay(800);
            await LoadSpecialsAsync();

            await ApiService.SaveSessionAsync("Store");
        }

        // ─────────────────────────────────────────────────────────────────
        //  FEATURED — 8 hier, cache-first, bez opakovaného volania API
        //  8 games, cache-first, no repeated API calls
        // ─────────────────────────────────────────────────────────────────
        private async Task LoadFeaturedAsync()
        {
            try
            {
                // 1. LocalCache hit → okamžité zobrazenie
                var cached = await LocalCacheService.LoadAsync<List<GameModel>>(
                    LocalCacheService.KeyFeatured, LocalCacheService.StoreTTL);

                if (cached != null && cached.Count >= 3)
                {
                    foreach (var g in cached) FeaturedGames.Add(g);
                    SetFeaturedGame(0);
                    _ = PreloadFeaturedImagesAsync(cached);
                    return;
                }

                // 2. Nový fetch — backend vráti plné detaily priamo
                //    Backend now returns full details directly (no per-game extra call needed)
                var libraryGenres = await GetLibraryGenresAsync();
                var candidates = await ApiService.GetFeaturedAsync();

                if (candidates.Count == 0) return;

                var result = new List<GameModel>();

                foreach (var game in candidates.Take(FeaturedTarget))
                {
                    // Možno už je v StoreGameCache z inej sekcie
                    // Maybe already in StoreGameCache from another section
                    var fromDisk = await StoreGameCacheService.LoadAsync(game.AppId);
                    var final = fromDisk ?? game;

                    // Nastav odporúčanie podľa knižnice
                    bool recommended = final.Genres?.Any(g =>
                        libraryGenres.Contains(g, StringComparer.OrdinalIgnoreCase)) ?? false;
                    final.StatusText = recommended ? "Recommended" : "Available Now";
                    final.IsRecommended= recommended;

                    result.Add(final);
                    FeaturedGames.Add(final);

                    // Zobraz prvú hru hneď ako je k dispozícii
                    if (FeaturedGames.Count == 1) SetFeaturedGame(0);

                    // Ulož na disk ak ešte nie je
                    if (fromDisk == null)
                        await StoreGameCacheService.SaveAsync(final);
                }

                if (result.Count > 0)
                {
                    await LocalCacheService.SaveAsync(LocalCacheService.KeyFeatured, result);
                    _ = PreloadFeaturedImagesAsync(result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Featured] Error: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  GENERICKÁ SEKCIA — cache-first, per-game disk cache
        //  Generic section — cache-first, per-game disk cache
        // ─────────────────────────────────────────────────────────────────
        private async Task LoadSectionAsync(
            ObservableCollection<GameModel> collection,
            string cacheKey,
            Func<int, Task<List<GameModel>>> fetchPage,
            Action<bool> setLoading,
            Action<bool> setHasMore)
        {
            setLoading(true);
            try
            {
                // 1. Skús LocalCache pre celú sekciu (zoznam hier)
                var cached = await LocalCacheService.LoadAsync<List<GameModel>>(
                    cacheKey, LocalCacheService.StoreTTL);

                if (cached != null && cached.Count > 0)
                {
                    foreach (var g in cached) collection.Add(g);
                    setHasMore(cached.Count >= PageSize);
                    _ = PreloadListImagesAsync(cached);
                    return;
                }

                // 2. Fetch stránky 0 z API
                var games = await fetchPage(0);
                if (games.Count == 0) return;

                var toSave = new List<GameModel>();

                foreach (var game in games)
                {
                    // Skontroluj per-game cache — môže byť z Featured alebo inej sekcie
                    // Check per-game cache — might be from Featured or another section
                    var fromDisk = await StoreGameCacheService.LoadAsync(game.AppId);
                    var final = fromDisk ?? game;

                    collection.Add(final);
                    toSave.Add(final);

                    if (fromDisk == null)
                        await StoreGameCacheService.SaveAsync(final);
                }

                setHasMore(games.Count >= PageSize);
                await LocalCacheService.SaveAsync(cacheKey, toSave);
                _ = PreloadListImagesAsync(toSave);
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

        // ─────────────────────────────────────────────────────────────────
        //  SHOW MORE — načíta +5 ďalších hier z ďalšej stránky
        //  Loads +5 more games from next page
        // ─────────────────────────────────────────────────────────────────
        private async Task ShowMoreAsync(ObservableCollection<GameModel> collection, int page, Func<int, Task<List<GameModel>>> fetchPage, Action<bool> setLoading, Action<bool> setHasMore)
        {
            setLoading(true);
            try
            {
                var games = await fetchPage(page);
                if (games.Count == 0) { setHasMore(false); return; }

                // Odfiltruj hry ktoré už sú v kolekcii — no duplicates
                var existingIds = new HashSet<int>(collection.Select(g => g.AppId));

                foreach (var game in games)
                {
                    if (existingIds.Contains(game.AppId)) continue;

                    var fromDisk = await StoreGameCacheService.LoadAsync(game.AppId);
                    var final = fromDisk ?? game;

                    collection.Add(final);
                    existingIds.Add(final.AppId);

                    if (fromDisk == null)
                        await StoreGameCacheService.SaveAsync(final);
                }

                setHasMore(games.Count >= PageSize);
                _ = PreloadListImagesAsync(games);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Store] ShowMore error: {ex.Message}");
                page--; // Rollback pri chybe
            }
            finally
            {
                setLoading(false);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  NEW TRENDING
        // ─────────────────────────────────────────────────────────────────
        private Task LoadNewTrendingAsync() => LoadSectionAsync(
            NewTrending,
            LocalCacheService.KeyTrending,
            page => ApiService.GetNewTrendingAsync(page),
            l => IsLoadingNewTrending = l,
            h => HasMoreNewTrending = h);

        [RelayCommand]
        private async Task ShowMoreNewTrendingAsync()
        {
            _newTrendingPage++;  // zvýš TU, pred volaním
            await ShowMoreAsync(NewTrending, _newTrendingPage,
                page => ApiService.GetNewTrendingAsync(page),
                l => IsLoadingNewTrending = l,
                h => HasMoreNewTrending = h);
        }

        // ─────────────────────────────────────────────────────────────────
        //  TOP SELLERS
        // ─────────────────────────────────────────────────────────────────
        private Task LoadTopSellersAsync() => LoadSectionAsync(
            TopSellers,
            LocalCacheService.KeyTopSellers,
            page => ApiService.GetTopSellersAsync(page),
            l => IsLoadingTopSellers = l,
            h => HasMoreTopSellers = h);

        [RelayCommand]
        private async Task ShowMoreTopSellersAsync()
        {
            _topSellersPage++;
            await ShowMoreAsync(TopSellers, _topSellersPage,
                page => ApiService.GetTopSellersAsync(page),
                l => IsLoadingTopSellers = l,
                h => HasMoreTopSellers = h);
        }

        // ─────────────────────────────────────────────────────────────────
        //  SPECIALS
        // ─────────────────────────────────────────────────────────────────
        private Task LoadSpecialsAsync() => LoadSectionAsync(
            Specials,
            LocalCacheService.KeySpecials,
            page => ApiService.GetSpecialsAsync(page),
            l => IsLoadingSpecials = l,
            h => HasMoreSpecials = h);

        [RelayCommand]
        private async Task ShowMoreSpecialsAsync()
        {
            _specialsPage++;
            await ShowMoreAsync(Specials, _specialsPage,
                page => ApiService.GetSpecialsAsync(page),
                l => IsLoadingSpecials = l,
                h => HasMoreSpecials = h);
        }

        // ─────────────────────────────────────────────────────────────────
        //  IMAGE PRELOADING
        // ─────────────────────────────────────────────────────────────────
        private async Task PreloadFeaturedImagesAsync(List<GameModel> games)
        {
            foreach (var game in games)
            {
                await PreloadSingleGameImagesAsync(game);
                await Task.Delay(100);
            }
        }

        private static async Task PreloadSingleGameImagesAsync(GameModel game)
        {
            if (!string.IsNullOrEmpty(game.HeaderImageUrl))
                await ImageCacheService.GetAsync(game.HeaderImageUrl);

            // Prednahrá prvé 4 screenshoty
            foreach (var shot in (game.Screenshots ?? new()).Take(4))
            {
                if (string.IsNullOrEmpty(shot)) continue;
                await ImageCacheService.GetAsync(shot);
                await Task.Delay(80);
            }
        }

        private static async Task PreloadListImagesAsync(List<GameModel> games)
        {
            foreach (var g in games)
            {
                if (string.IsNullOrEmpty(g.HeaderImageUrl)) continue;
                try { await ImageCacheService.GetAsync(g.HeaderImageUrl); }
                catch { }
                await Task.Delay(60);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  SET FEATURED GAME — nastaví aktívnu hru v carouseli
        //  Sets the active game in the carousel
        // ─────────────────────────────────────────────────────────────────
        private void SetFeaturedGame(int index)
        {
            if (FeaturedGames.Count == 0) return;
            index = Math.Clamp(index, 0, FeaturedGames.Count - 1);

            FeaturedIndex = index;
            CurrentFeaturedGame = FeaturedGames[index];

            var game = FeaturedGames[index];
            var shots = game.Screenshots ?? new List<string>();

            FeaturedMainImage = new GameImageViewModel(game.HeaderImageUrl ?? "");
            FeaturedScreenshot1 = new GameImageViewModel(shots.ElementAtOrDefault(0) ?? "");
            FeaturedScreenshot2 = new GameImageViewModel(shots.ElementAtOrDefault(1) ?? "");
            FeaturedScreenshot3 = new GameImageViewModel(shots.ElementAtOrDefault(2) ?? "");
            FeaturedScreenshot4 = new GameImageViewModel(shots.ElementAtOrDefault(3) ?? "");
        }

        // ─────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────
        private static async Task<HashSet<string>> GetLibraryGenresAsync()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!SessionManager.IsLoggedIn) return result;
                var library = await ApiService.GetLibraryAsync(SessionManager.SteamId64);
                foreach (var game in library)
                    foreach (var genre in game.Genres ?? new())
                        result.Add(genre);
            }
            catch { }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        //  CAROUSEL COMMANDS
        // ─────────────────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────────────────
        //  SELECT GAME
        // ─────────────────────────────────────────────────────────────────
        [RelayCommand]
        private void SelectGame(GameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game);
        }
    }
}