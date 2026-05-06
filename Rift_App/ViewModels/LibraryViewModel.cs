using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        public ObservableCollection<GameModel> Games { get; } = new();
        public ObservableCollection<GameModel> FilteredGames { get; } = new();

        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalGames = 0;

        // Fired when user clicks a game — Library.xaml.cs forwards to GameDetailPanel
        public event Action<GameModel>? OnGameSelected;

        // ─── SELECT GAME ──────────────────────────────────────────────────

        [RelayCommand]
        private void SelectGame(GameModel game)
        {
            if (game != null) OnGameSelected?.Invoke(game);
        }

        // ─── LOAD — disk cache first ──────────────────────────────────────

        [RelayCommand]
        public async Task LoadLibraryAsync()
        {
            IsLoading = true;
            Games.Clear();
            FilteredGames.Clear();

            try
            {
                // 1. Load from disk cache — instant startup
                var cached = await LibraryCacheService.LoadAsync();

                if (cached != null && cached.Count > 0)
                {
                    RestoreIconPaths(cached);
                    PopulateGames(cached);
                    IsLoading = false;

                    // Check for added/removed games in background
                    _ = SyncInBackgroundAsync();
                    return;
                }

                // 2. First run — fetch from API and download all icons
                var games = await ApiService.GetLibraryAsync(SessionManager.SteamId64);
                if (games.Count == 0) return;

                var sorted = games.OrderByDescending(g => g.PlaytimeMinutes).ToList();
                await LibraryCacheService.DownloadAllIconsAsync(sorted);
                await LibraryCacheService.SaveAsync(sorted);
                PopulateGames(sorted);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Library] Load error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                await ApiService.SaveSessionAsync("Library");
            }
        }

        // ─── BACKGROUND SYNC — adds new, removes deleted games ────────────

        private async Task SyncInBackgroundAsync()
        {
            try
            {
                await Task.Delay(3000);

                var fresh = await ApiService.GetLibraryAsync(SessionManager.SteamId64);
                if (fresh.Count == 0) return;

                var cached = Games.ToList();
                var (synced, changed) = await LibraryCacheService.SyncAsync(cached, fresh);
                if (!changed) return;

                RestoreIconPaths(synced);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Games.Clear();
                    FilteredGames.Clear();
                    PopulateGames(synced.OrderByDescending(g => g.PlaytimeMinutes).ToList());
                });

                await LibraryCacheService.SaveAsync(synced);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Library] Sync error: {ex.Message}");
            }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static void RestoreIconPaths(List<GameModel> games)
        {
            foreach (var game in games)
            {
                var path = LibraryCacheService.GetIconPath(game.AppId);
                game.IconPath = File.Exists(path) ? path : null;
            }
        }

        private void PopulateGames(List<GameModel> games)
        {
            foreach (var game in games.OrderByDescending(g => g.PlaytimeMinutes))
            {
                Games.Add(game);
                FilteredGames.Add(game);
            }
            TotalGames = Games.Count;
        }

        // ─── SEARCH ───────────────────────────────────────────────────────

        partial void OnSearchTextChanged(string value)
        {
            FilteredGames.Clear();
            var filtered = string.IsNullOrWhiteSpace(value)
                ? Games
                : Games.Where(g => g.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
            foreach (var game in filtered) FilteredGames.Add(game);
        }
    }
}