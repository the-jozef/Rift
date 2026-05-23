using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.Store;
using Rift_App.ViewModels;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rift_App.ViewModels
{
    public partial class StoreViewModel : ObservableObject
    {
        //───── FULL COLLECTIONS ─────────────────────────────────────────────────────
        public ObservableCollection<GameModel> FeaturedGames { get; } = new();
        public ObservableCollection<GameModel> DiscountGames { get; } = new();
        public ObservableCollection<GameModel> RecommendedGames { get; } = new();
        public ObservableCollection<GameModel> ByTagGames { get; } = new();
        public ObservableCollection<GameModel> MoreGames { get; } = new();

        //───── VISIBLE SLICES ─────────────────────────────────────────────────────

        // 8 of 24 discounts visible at once
        public ObservableCollection<GameModel> VisibleDiscounts { get; } = new();

        // 4 of 12 recommended visible at once
        public ObservableCollection<GameModel> VisibleRecommended { get; } = new();

        // 4 of 12 by-tag visible at once
        public ObservableCollection<GameModel> VisibleByTag { get; } = new();

        //───── PAGE SIZES ──────────────────────────────────────────────────────
        private const int DiscountPageSize = 8;
        private const int CardPageSize = 4;
        private const int MoreMaxPages = 5;

        //───── PAGE INDICES  (remembered — arrows go back to same position) ─────────────────────────────────────────────────────
        [ObservableProperty] private int _discountPageIndex;
        [ObservableProperty] private int _recommendedPageIndex;
        [ObservableProperty] private int _byTagPageIndex;

        //───── FEATURED CAROUSEL STATE ─────────────────────────────────────────────────────
        [ObservableProperty] private GameModel? _currentFeaturedGame;
        [ObservableProperty] private int _featuredIndex;
        [ObservableProperty] private GameImageViewModel? _featuredMainImage;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot1;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot2;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot3;
        [ObservableProperty] private GameImageViewModel? _featuredScreenshot4;

        //───── LOADING FLAGS ─────────────────────────────────────────────────────
        [ObservableProperty] private bool _isLoadingFeatured;
        [ObservableProperty] private bool _isLoadingDiscounts;
        [ObservableProperty] private bool _isLoadingRecommended;
        [ObservableProperty] private bool _isLoadingByTag;

        // MORE SECTION STATE═════════════════════════════════════════════════════════════════
        [ObservableProperty] private bool _hasMoreGames = true;
        [ObservableProperty] private bool _isLoadingMore = false;
        private int _morePage = -1;

        //───── BY-TAG HEADER ─────────────────────────────────────────────────────
        [ObservableProperty] private string _byTagLabel = "Because You Play...";

        //───── EVENT ─────────────────────────────────────────────────────
        public event Action<GameModel>? OnGameSelected;

        //───── MAIN LOAD — progressive, visible sections first ─────────────────────────────────────────────────────
        [RelayCommand]
        public async Task LoadStoreAsync()
        {
            await TagService.InitAsync();

            await LoadFeaturedAsync();
            await LoadDiscountsAsync();

            var genres = await GetLibraryGenresAsync();
            await Task.WhenAll(
                LoadRecommendedAsync(genres),
                LoadByTagAsync(genres));

            // Pre-load first 5 "more" games so the section isn't empty on open
            await ShowMoreAsync();

            _ = ApiService.SaveSessionAsync("Store");
        }

        //───── FEATURED ─────────────────────────────────────────────────────
        private async Task LoadFeaturedAsync()
        {
            IsLoadingFeatured = true;
            try
            {
                var cached = await StoreGameCacheService.LoadSectionAsync(StoreGameCacheService.KeyFeatured);
                if (cached != null && cached.Count >= 3)
                {
                    PopulateCollection(FeaturedGames, cached);
                    SetFeaturedGame(0);
                    _ = PreloadImagesAsync(cached);
                    return;
                }

                var games = await ApiService.GetFeaturedAsync();
                if (games.Count == 0) return;

                PopulateCollection(FeaturedGames, games);
                SetFeaturedGame(0);
                await StoreGameCacheService.SaveSectionAsync(StoreGameCacheService.KeyFeatured, games);
                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex) { Debug.WriteLine($"[StoreVM] Featured: {ex.Message}"); }
            finally { IsLoadingFeatured = false; }
        }
 
        //───── DISCOUNTS ─────────────────────────────────────────────────────
        private async Task LoadDiscountsAsync()
        {
            IsLoadingDiscounts = true;
            try
            {
                var cached = await StoreGameCacheService.LoadSectionAsync(StoreGameCacheService.KeyDiscounts);
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
                await StoreGameCacheService.SaveSectionAsync(StoreGameCacheService.KeyDiscounts, games);
                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex) { Debug.WriteLine($"[StoreVM] Discounts: {ex.Message}"); }
            finally { IsLoadingDiscounts = false; }
        }

        //───── RECOMMENDED ─────────────────────────────────────────────────────
        private async Task LoadRecommendedAsync(HashSet<string> genres)
        {
            IsLoadingRecommended = true;
            try
            {
                var cached = await StoreGameCacheService.LoadSectionAsync(StoreGameCacheService.KeyRecommended);
                if (cached != null && cached.Count >= 4)
                {
                    PopulateCollection(RecommendedGames, cached);
                    RefreshVisibleRecommended();
                    _ = PreloadImagesAsync(cached);
                    return;
                }

                var games = await ApiService.GetRecommendedAsync(genres);
                if (games.Count == 0) return;

                var usedIds = GetUsedIds();
                games = games.Where(g => !usedIds.Contains(g.AppId)).ToList();

                PopulateCollection(RecommendedGames, games);
                RefreshVisibleRecommended();
                await StoreGameCacheService.SaveSectionAsync(StoreGameCacheService.KeyRecommended, games);
                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex) { Debug.WriteLine($"[StoreVM] Recommended: {ex.Message}"); }
            finally { IsLoadingRecommended = false; }
        }

        //───── BY TAG ─────────────────────────────────────────────────────
        private async Task LoadByTagAsync(HashSet<string> genres)
        {
            IsLoadingByTag = true;
            try
            {
                var tag = PickBestTag(genres);
                var cacheKey = StoreGameCacheService.KeyByTag(tag);
                ByTagLabel = $"Because You Play {tag}";

                var cached = await StoreGameCacheService.LoadSectionAsync(cacheKey);
                if (cached != null && cached.Count >= 4)
                {
                    PopulateCollection(ByTagGames, cached);
                    RefreshVisibleByTag();
                    _ = PreloadImagesAsync(cached);
                    return;
                }

                var games = await ApiService.GetByTagAsync(tag);
                if (games.Count == 0) return;

                var usedIds = GetUsedIds();
                games = games.Where(g => !usedIds.Contains(g.AppId)).ToList();

                PopulateCollection(ByTagGames, games);
                RefreshVisibleByTag();
                await StoreGameCacheService.SaveSectionAsync(cacheKey, games);
                _ = PreloadImagesAsync(games);
            }
            catch (Exception ex) { Debug.WriteLine($"[StoreVM] ByTag: {ex.Message}"); }
            finally { IsLoadingByTag = false; }
        }

        // ─── SHOW MORE ────────────────────────────────────────────────────
        [RelayCommand]
        private async Task ShowMoreAsync()
        {
            if (IsLoadingMore || !HasMoreGames) return;
            IsLoadingMore = true;
            try
            {
                _morePage++;
                if (_morePage >= MoreMaxPages) { HasMoreGames = false; return; }

                var cacheKey = $"{StoreGameCacheService.KeyMore}_p{_morePage}";

                // Try cache first
                var cached = await StoreGameCacheService.LoadSectionAsync(cacheKey);

                List<GameModel> games;
                bool fromApi = false;

                if (cached != null && cached.Count > 0)
                {
                    games = cached;
                }
                else
                {
                    var result = await ApiService.GetMoreAsync(_morePage);
                    games = result.Games;
                    fromApi = true;
                    HasMoreGames = result.HasMore && (_morePage + 1) < MoreMaxPages;
                }

                if (games.Count == 0) { HasMoreGames = false; return; }

                // Only exclude Featured and Discounts — NOT Recommended/ByTag.
                // This keeps More well-populated even if the tag/recommended games overlap.
                var exclusions = GetMoreExclusions();
                games = games.Where(g => !exclusions.Contains(g.AppId)).ToList();

                foreach (var g in games)
                    MoreGames.Add(g);

                if (fromApi)
                    await StoreGameCacheService.SaveSectionAsync(cacheKey, games);

                _ = PreloadImagesAsync(games);

                if (_morePage + 1 >= MoreMaxPages) HasMoreGames = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreVM] ShowMore: {ex.Message}");
                _morePage--; // allow retry
            }
            finally { IsLoadingMore = false; }
        }

        //───── CAROUSEL COMMANDS — DISCOUNTS ─────────────────────────────────────────────────────
        [RelayCommand]
        private void NextDiscountPage()
        {
            if (DiscountGames.Count == 0) return;
            int total = (int)Math.Ceiling(DiscountGames.Count / (double)DiscountPageSize);
            DiscountPageIndex = (DiscountPageIndex + 1) % total;
            RefreshVisibleDiscounts();
        }

        [RelayCommand]
        private void PrevDiscountPage()
        {
            if (DiscountGames.Count == 0) return;
            int total = (int)Math.Ceiling(DiscountGames.Count / (double)DiscountPageSize);
            DiscountPageIndex = (DiscountPageIndex - 1 + total) % total;
            RefreshVisibleDiscounts();
        }

        private void RefreshVisibleDiscounts()
        {
            VisibleDiscounts.Clear();
            foreach (var g in DiscountGames.Skip(DiscountPageIndex * DiscountPageSize).Take(DiscountPageSize))
                VisibleDiscounts.Add(g);
        }
 
        //───── CAROUSEL COMMANDS — RECOMMENDED ─────────────────────────────────────────────────────
        [RelayCommand]
        private void NextRecommended()
        {
            if (RecommendedGames.Count == 0) return;
            int total = (int)Math.Ceiling(RecommendedGames.Count / (double)CardPageSize);
            RecommendedPageIndex = (RecommendedPageIndex + 1) % total;
            RefreshVisibleRecommended();
        }

        [RelayCommand]
        private void PrevRecommended()
        {
            if (RecommendedGames.Count == 0) return;
            int total = (int)Math.Ceiling(RecommendedGames.Count / (double)CardPageSize);
            RecommendedPageIndex = (RecommendedPageIndex - 1 + total) % total;
            RefreshVisibleRecommended();
        }

        private void RefreshVisibleRecommended()
        {
            VisibleRecommended.Clear();
            foreach (var g in RecommendedGames.Skip(RecommendedPageIndex * CardPageSize).Take(CardPageSize))
                VisibleRecommended.Add(g);
        }

        //───── CAROUSEL COMMANDS — BY TAG ─────────────────────────────────────────────────────
        [RelayCommand]
        private void NextByTag()
        {
            if (ByTagGames.Count == 0) return;
            int total = (int)Math.Ceiling(ByTagGames.Count / (double)CardPageSize);
            ByTagPageIndex = (ByTagPageIndex + 1) % total;
            RefreshVisibleByTag();
        }

        [RelayCommand]
        private void PrevByTag()
        {
            if (ByTagGames.Count == 0) return;
            int total = (int)Math.Ceiling(ByTagGames.Count / (double)CardPageSize);
            ByTagPageIndex = (ByTagPageIndex - 1 + total) % total;
            RefreshVisibleByTag();
        }

        private void RefreshVisibleByTag()
        {
            VisibleByTag.Clear();
            foreach (var g in ByTagGames.Skip(ByTagPageIndex * CardPageSize).Take(CardPageSize))
                VisibleByTag.Add(g);
        }

        //───── CAROUSEL COMMANDS — FEATURED ─────────────────────────────────────────────────────
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
 
        //───── SELECT GAME ─────────────────────────────────────────────────────
        [RelayCommand]
        private void SelectGame(GameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game);

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
                Debug.WriteLine($"[SearchBar] Open steam error: {ex.Message}");
            }
        }

        //───── HELPERS ─────────────────────────────────────────────────────

        private HashSet<int> GetMoreExclusions() =>
            new(FeaturedGames.Select(g => g.AppId)
                .Concat(DiscountGames.Select(g => g.AppId)));

        // Keep these existing helpers unchanged:
        private HashSet<int> GetUsedIds() =>
            new(FeaturedGames.Select(g => g.AppId)
                .Concat(DiscountGames.Select(g => g.AppId)));

        private HashSet<int> GetAllUsedIds() =>
            new(FeaturedGames.Select(g => g.AppId)
                .Concat(DiscountGames.Select(g => g.AppId))
                .Concat(RecommendedGames.Select(g => g.AppId))
                .Concat(ByTagGames.Select(g => g.AppId))
                .Concat(MoreGames.Select(g => g.AppId)));
        private static void PopulateCollection(ObservableCollection<GameModel> col, List<GameModel> games)
        {
            col.Clear();
            foreach (var g in games) col.Add(g);
        }
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

        private static readonly string[] KnownTags =
            { "Action", "RPG", "Survival", "Multiplayer", "Strategy", "Horror", "Indie", "Simulation" };

        private static string PickBestTag(HashSet<string> genres)
        {
            foreach (var tag in KnownTags)
                if (genres.Contains(tag)) return tag;
            foreach (var genre in genres)
                foreach (var tag in KnownTags)
                    if (genre.Contains(tag, StringComparison.OrdinalIgnoreCase)) return tag;
            return "Action";
        }

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