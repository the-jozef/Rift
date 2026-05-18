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
        // ═════════════════════════════════════════════════════════════════
        //  COLLECTIONS — one per store section
        // ═════════════════════════════════════════════════════════════════

        /// 8 games shown in the main featured carousel at the top.
        public ObservableCollection<GameModel> FeaturedGames { get; } = new();

        /// 24 discounted games — arrows show 8 at a time (client-side pagination).
        public ObservableCollection<GameModel> DiscountGames { get; } = new();

        /// 12 games recommended based on the player's library genres.
        public ObservableCollection<GameModel> RecommendedGames { get; } = new();

        /// 12 games matching a specific tag from the player's library.
        public ObservableCollection<GameModel> ByTagGames { get; } = new();

        /// "More" section — starts empty, grows 5 per click, max 25.
        public ObservableCollection<GameModel> MoreGames { get; } = new();

        // ═════════════════════════════════════════════════════════════════
        //  FEATURED CAROUSEL STATE
        // ═════════════════════════════════════════════════════════════════

        [ObservableProperty] private GameModel? _currentFeaturedGame;
        [ObservableProperty] private int _featuredIndex;
        [ObservableProperty] private GameImageViewModel? _featuredMainImage;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot1;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot2;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot3;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot4;

        // ═════════════════════════════════════════════════════════════════
        //  DISCOUNT CAROUSEL STATE (client-side, no network on arrow click)
        // ═════════════════════════════════════════════════════════════════

        [ObservableProperty] private int _discountPageIndex = 0;

        /// Slice of DiscountGames currently visible (8 games).
        public ObservableCollection<GameModel> VisibleDiscounts { get; } = new();

        private const int DiscountPageSize = 8;

        // ═════════════════════════════════════════════════════════════════
        //  "MORE" SECTION STATE
        // ═════════════════════════════════════════════════════════════════

        [ObservableProperty] private bool _hasMoreGames = true;
        [ObservableProperty] private bool _isLoadingMore = false;
        private int _morePage = -1;   // -1 = not yet loaded
        private const int MoreMaxPages = 5;

        // ═════════════════════════════════════════════════════════════════
        //  LOADING FLAGS
        // ═════════════════════════════════════════════════════════════════

        [ObservableProperty] private bool _isLoadingFeatured;
        [ObservableProperty] private bool _isLoadingDiscounts;
        [ObservableProperty] private bool _isLoadingRecommended;
        [ObservableProperty] private bool _isLoadingByTag;

        // ═════════════════════════════════════════════════════════════════
        //  BY-TAG LABEL  (shown in header: "Because you play Action")
        // ═════════════════════════════════════════════════════════════════

        [ObservableProperty] private string _byTagLabel = "Popular in Your Genre";

        // ═════════════════════════════════════════════════════════════════
        //  EVENTS
        // ═════════════════════════════════════════════════════════════════

        public event Action<GameModel>? OnGameSelected;

        // ═════════════════════════════════════════════════════════════════
        //  MAIN LOAD  — progressive, visible sections first
        // ═════════════════════════════════════════════════════════════════

        [RelayCommand]
        public async Task LoadStoreAsync()
        {
            await TagService.InitAsync();

            // 1. Featured first — user sees something immediately
            await LoadFeaturedAsync();

            // 2. Discounts — big section, load early so arrows work
            await LoadDiscountsAsync();

            // 3. Recommended + by-tag in parallel (both need library genres)
            var genres = await GetLibraryGenresAsync();
            await Task.WhenAll(
                LoadRecommendedAsync(genres),
                LoadByTagAsync(genres));

            // 4. Save session in background
            _ = ApiService.SaveSessionAsync("Store");
        }

        // ═════════════════════════════════════════════════════════════════
        //  FEATURED  — 8 games, carousel
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadFeaturedAsync()
        {
            IsLoadingFeatured = true;
            try
            {
                // Try disk cache first
                var cached = await StoreGameCacheService.LoadSectionAsync(
                    StoreGameCacheService.KeyFeatured);

                if (cached != null && cached.Count >= 3)
                {
                    PopulateCollection(FeaturedGames, cached);
                    SetFeaturedGame(0);
                    _ = PreloadImagesAsync(cached);
                    return;
                }

                // Fetch from API
                var games = await ApiService.GetFeaturedAsync();
                if (games.Count == 0) return;

                PopulateCollection(FeaturedGames, games);
                SetFeaturedGame(0);

                // Save to cache (images downloaded inside SaveSectionAsync)
                await StoreGameCacheService.SaveSectionAsync(
                    StoreGameCacheService.KeyFeatured, games);

                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreVM] Featured error: {ex.Message}");
            }
            finally
            {
                IsLoadingFeatured = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  DISCOUNTS  — 24 games, arrows switch 8 at a time (client-side)
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadDiscountsAsync()
        {
            IsLoadingDiscounts = true;
            try
            {
                var cached = await StoreGameCacheService.LoadSectionAsync(
                    StoreGameCacheService.KeyDiscounts);

                if (cached != null && cached.Count >= 8)
                {
                    PopulateCollection(DiscountGames, cached);
                    RefreshVisibleDiscounts();
                    _ = PreloadImagesAsync(cached);
                    return;
                }

                var games = await ApiService.GetDiscountsAsync();
                if (games.Count == 0) return;

                PopulateCollection(DiscountGames, games);
                RefreshVisibleDiscounts();

                await StoreGameCacheService.SaveSectionAsync(
                    StoreGameCacheService.KeyDiscounts, games);

                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreVM] Discounts error: {ex.Message}");
            }
            finally
            {
                IsLoadingDiscounts = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  RECOMMENDED  — 12 games based on library genres
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadRecommendedAsync(HashSet<string> genres)
        {
            IsLoadingRecommended = true;
            try
            {
                var cached = await StoreGameCacheService.LoadSectionAsync(
                    StoreGameCacheService.KeyRecommended);

                if (cached != null && cached.Count >= 4)
                {
                    PopulateCollection(RecommendedGames, cached);
                    _ = PreloadImagesAsync(cached);
                    return;
                }

                var games = await ApiService.GetRecommendedAsync(genres);
                if (games.Count == 0) return;

                // Remove any already shown in Featured / Discounts
                var usedIds = GetUsedIds();
                games = games.Where(g => !usedIds.Contains(g.AppId)).ToList();

                PopulateCollection(RecommendedGames, games);

                await StoreGameCacheService.SaveSectionAsync(
                    StoreGameCacheService.KeyRecommended, games);

                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreVM] Recommended error: {ex.Message}");
            }
            finally
            {
                IsLoadingRecommended = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  BY TAG  — 12 games matching player's top genre
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadByTagAsync(HashSet<string> genres)
        {
            IsLoadingByTag = true;
            try
            {
                // Pick the most relevant tag from player's library
                var tag = PickBestTag(genres);
                var cacheKey = StoreGameCacheService.KeyByTag(tag);

                ByTagLabel = $"Because You Play {tag}";

                var cached = await StoreGameCacheService.LoadSectionAsync(cacheKey);
                if (cached != null && cached.Count >= 4)
                {
                    PopulateCollection(ByTagGames, cached);
                    _ = PreloadImagesAsync(cached);
                    return;
                }

                var games = await ApiService.GetByTagAsync(tag);
                if (games.Count == 0) return;

                // Remove duplicates from other sections
                var usedIds = GetUsedIds();
                games = games.Where(g => !usedIds.Contains(g.AppId)).ToList();

                PopulateCollection(ByTagGames, games);

                await StoreGameCacheService.SaveSectionAsync(cacheKey, games);
                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreVM] ByTag error: {ex.Message}");
            }
            finally
            {
                IsLoadingByTag = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  MORE — 5 per click, max 25 total
        // ═════════════════════════════════════════════════════════════════

        [RelayCommand]
        private async Task ShowMoreAsync()
        {
            if (IsLoadingMore || !HasMoreGames) return;

            IsLoadingMore = true;
            try
            {
                _morePage++;

                if (_morePage >= MoreMaxPages)
                {
                    HasMoreGames = false;
                    return;
                }

                // Check disk cache for this page
                var cacheKey = $"{StoreGameCacheService.KeyMore}_p{_morePage}";
                var cached = await StoreGameCacheService.LoadSectionAsync(cacheKey);

                List<GameModel> games;
                bool fetchedFromApi = false;

                if (cached != null && cached.Count > 0)
                {
                    games = cached;
                }
                else
                {
                    var result = await ApiService.GetMoreAsync(_morePage);
                    games = result.Games;
                    HasMoreGames = result.HasMore && (_morePage + 1) < MoreMaxPages;
                    fetchedFromApi = true;
                }

                if (games.Count == 0)
                {
                    HasMoreGames = false;
                    return;
                }

                // Remove duplicates from ALL other sections
                var usedIds = GetAllUsedIds();
                games = games.Where(g => !usedIds.Contains(g.AppId)).ToList();

                foreach (var game in games)
                    MoreGames.Add(game);

                if (fetchedFromApi)
                    await StoreGameCacheService.SaveSectionAsync(cacheKey, games);

                _ = PreloadImagesAsync(games);

                // Hide button after last page
                if (_morePage + 1 >= MoreMaxPages)
                    HasMoreGames = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreVM] ShowMore error: {ex.Message}");
                _morePage--; // rollback on error
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  DISCOUNT CAROUSEL COMMANDS  (client-side, instant)
        // ═════════════════════════════════════════════════════════════════

        [RelayCommand]
        private void NextDiscountPage()
        {
            if (DiscountGames.Count == 0) return;
            int totalPages = (int)Math.Ceiling(DiscountGames.Count / (double)DiscountPageSize);
            DiscountPageIndex = (DiscountPageIndex + 1) % totalPages;
            RefreshVisibleDiscounts();
        }

        [RelayCommand]
        private void PrevDiscountPage()
        {
            if (DiscountGames.Count == 0) return;
            int totalPages = (int)Math.Ceiling(DiscountGames.Count / (double)DiscountPageSize);
            DiscountPageIndex = (DiscountPageIndex - 1 + totalPages) % totalPages;
            RefreshVisibleDiscounts();
        }

        private void RefreshVisibleDiscounts()
        {
            VisibleDiscounts.Clear();
            var page = DiscountGames
                .Skip(DiscountPageIndex * DiscountPageSize)
                .Take(DiscountPageSize);
            foreach (var g in page)
                VisibleDiscounts.Add(g);
        }

        // ═════════════════════════════════════════════════════════════════
        //  FEATURED CAROUSEL COMMANDS
        // ═════════════════════════════════════════════════════════════════

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

        // ═════════════════════════════════════════════════════════════════
        //  SELECT GAME  (navigate to game page)
        // ═════════════════════════════════════════════════════════════════

        [RelayCommand]
        private void SelectGame(GameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        /// Replaces collection contents without clearing and re-adding one by one
        /// (reduces UI flicker).
        private static void PopulateCollection(ObservableCollection<GameModel> col,
                                               List<GameModel> games)
        {
            col.Clear();
            foreach (var g in games)
                col.Add(g);
        }

        /// AppIds already used in Featured + Discounts (for deduplication).
        private HashSet<int> GetUsedIds() =>
            new(FeaturedGames.Select(g => g.AppId)
                .Concat(DiscountGames.Select(g => g.AppId)));

        /// AppIds used across ALL sections (for More deduplication).
        private HashSet<int> GetAllUsedIds() =>
            new(FeaturedGames.Select(g => g.AppId)
                .Concat(DiscountGames.Select(g => g.AppId))
                .Concat(RecommendedGames.Select(g => g.AppId))
                .Concat(ByTagGames.Select(g => g.AppId))
                .Concat(MoreGames.Select(g => g.AppId)));

        /// Gets the player's library genres for personalisation.
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

        /// Picks the best matching tag from the known tag map.
        private static readonly string[] KnownTags =
            { "Action", "RPG", "Survival", "Multiplayer", "Strategy", "Horror", "Indie", "Simulation" };

        private static string PickBestTag(HashSet<string> genres)
        {
            foreach (var tag in KnownTags)
                if (genres.Contains(tag)) return tag;

            // Fallback — pick first genre that maps to a known tag
            foreach (var genre in genres)
                foreach (var tag in KnownTags)
                    if (genre.Contains(tag, StringComparison.OrdinalIgnoreCase)) return tag;

            return "Action"; // final fallback
        }

        /// Preloads images into ImageCacheService in the background.
        private static async Task PreloadImagesAsync(List<GameModel> games)
        {
            foreach (var g in games)
            {
                try
                {
                    if (!string.IsNullOrEmpty(g.HeaderImageUrl))
                        await ImageCacheService.GetAsync(g.HeaderImageUrl);

                    foreach (var shot in (g.Screenshots ?? new()).Take(4))
                    {
                        if (!string.IsNullOrEmpty(shot))
                            await ImageCacheService.GetAsync(shot);
                        await Task.Delay(60);
                    }
                }
                catch { }
                await Task.Delay(80);
            }
        }
    }
}