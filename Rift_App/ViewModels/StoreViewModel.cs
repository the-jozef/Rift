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

        public string Screenshot1 => CurrentFeaturedGame?.Screenshots?.ElementAtOrDefault(0) ?? string.Empty;
        public string Screenshot2 => CurrentFeaturedGame?.Screenshots?.ElementAtOrDefault(1) ?? string.Empty;
        public string Screenshot3 => CurrentFeaturedGame?.Screenshots?.ElementAtOrDefault(2) ?? string.Empty;
        public string Screenshot4 => CurrentFeaturedGame?.Screenshots?.ElementAtOrDefault(3) ?? string.Empty;

        public event Action<GameModel>? OnGameSelected;

        // ─── LOAD ALL ─────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadStoreAsync()
        {
            await LoadFeaturedAsync();
            await Task.Delay(3000);
            await LoadNewTrendingAsync();
            await Task.Delay(3000);
            await LoadTopSellersAsync();
            await Task.Delay(3000);
            await LoadSpecialsAsync();
            await ApiService.SaveSessionAsync("Store");
        }

        // ─── FEATURED — loop dokedy nemá 8 hier ───────────────────────────
        // Loop until we have 8 games or exhausted retries

        private async Task LoadFeaturedAsync()
        {
            const int target = 8;
            const int maxAttempts = 5;

            var libraryGenres = await GetLibraryGenresAsync();

            // Skús featured endpoint, fallback na newtrending
            // Try featured endpoint, fallback to newtrending
            List<GameModel> candidates = await ApiService.GetFeaturedAsync();
            if (candidates.Count == 0)
                candidates = await ApiService.GetNewTrendingAsync(0);

            int attempt = 0;

            // Loop — opakuj kým nemáme target alebo sme vyskúšali max
            // Loop — repeat until target reached or max attempts done
            while (FeaturedGames.Count < target && attempt < maxAttempts)
            {
                attempt++;

                foreach (var candidate in candidates)
                {
                    if (FeaturedGames.Count >= target) break;

                    // Preskočiť ak už je v zozname — skip if already in list
                    if (FeaturedGames.Any(g => g.AppId == candidate.AppId)) continue;

                    // Donačítaj plné detaily (screenshoty, popis, 18+ check na backende)
                    // Load full details — backend returns 451 for adult content
                    var full = await ApiService.GetGameDetailsAsync(candidate.AppId);
                    if (full == null)
                    {
                        // null = 18+ (451) alebo iná chyba — preskočiť
                        // null = 18+ (451) or other error — skip
                        await Task.Delay(1500);
                        continue;
                    }

                    // Recommended ak sa žánre zhodujú s knižnicou
                    // Recommended if genres match user library
                    bool recommended = full.Genres.Any(g =>
                        libraryGenres.Contains(g, StringComparer.OrdinalIgnoreCase));
                    full.StatusText = recommended ? "Recommended" : "Available Now";

                    FeaturedGames.Add(full);

                    if (FeaturedGames.Count == 1)
                        SetFeaturedGame(0);

                    // Pauza medzi hrami — pause between games
                    await Task.Delay(2000);
                }

                // Ak stále nemáme dosť — skús ďalšiu stránku
                // If still not enough — try next page
                if (FeaturedGames.Count < target)
                {
                    await Task.Delay(5000);
                    candidates = await ApiService.GetNewTrendingAsync(attempt);
                }
            }
        }

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

        // ─── NEW TRENDING ─────────────────────────────────────────────────

        private async Task LoadNewTrendingAsync()
        {
            IsLoadingNewTrending = true;
            try
            {
                var g = await ApiService.GetNewTrendingAsync(_newTrendingPage);
                foreach (var game in g) NewTrending.Add(game);
                if (g.Count < 10) HasMoreNewTrending = false;
            }
            catch { }
            finally { IsLoadingNewTrending = false; }
        }

        [RelayCommand]
        private async Task ShowMoreNewTrendingAsync()
        {
            _newTrendingPage++;
            await LoadNewTrendingAsync();
        }

        // ─── TOP SELLERS ──────────────────────────────────────────────────

        private async Task LoadTopSellersAsync()
        {
            IsLoadingTopSellers = true;
            try
            {
                var g = await ApiService.GetTopSellersAsync(_topSellersPage);
                foreach (var game in g) TopSellers.Add(game);
                if (g.Count < 10) HasMoreTopSellers = false;
            }
            catch { }
            finally { IsLoadingTopSellers = false; }
        }

        [RelayCommand]
        private async Task ShowMoreTopSellersAsync()
        {
            _topSellersPage++;
            await LoadTopSellersAsync();
        }

        // ─── SPECIALS ─────────────────────────────────────────────────────

        private async Task LoadSpecialsAsync()
        {
            IsLoadingSpecials = true;
            try
            {
                var g = await ApiService.GetSpecialsAsync(_specialsPage);
                foreach (var game in g) Specials.Add(game);
                if (g.Count < 10) HasMoreSpecials = false;
            }
            catch { }
            finally { IsLoadingSpecials = false; }
        }

        [RelayCommand]
        private async Task ShowMoreSpecialsAsync()
        {
            _specialsPage++;
            await LoadSpecialsAsync();
        }

        // ─── FEATURED CAROUSEL ────────────────────────────────────────────

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
            FeaturedIndex = index;
            CurrentFeaturedGame = FeaturedGames[index];
            OnPropertyChanged(nameof(Screenshot1));
            OnPropertyChanged(nameof(Screenshot2));
            OnPropertyChanged(nameof(Screenshot3));
            OnPropertyChanged(nameof(Screenshot4));
        }

        // ─── SELECT GAME ──────────────────────────────────────────────────

        [RelayCommand]
        private void SelectGame(GameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game);
        }
    }
}