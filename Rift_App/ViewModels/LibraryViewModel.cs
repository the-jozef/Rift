using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.GameModels;
using Rift_App.Image;
using Rift_App.Models;
using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Rift_App.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        public ObservableCollection<GameModel> Games { get; } = new();
        public ObservableCollection<GameModel> FilteredGames { get; } = new();

        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalGames = 0;

        public event Action<GameModel>? OnGameSelected;

        [RelayCommand]
        public async Task LoadLibraryAsync()
        {
            IsLoading = true; Games.Clear(); FilteredGames.Clear();
            try
            {
                var games = await ApiService.GetLibraryAsync(SessionManager.SteamId64);
                foreach (var game in games.OrderByDescending(g => g.PlaytimeMinutes))
                {
                    Games.Add(game); FilteredGames.Add(game);
                }
                TotalGames = Games.Count;
                await ApiService.SaveSessionAsync("Library");
            }
            catch { }
            finally { IsLoading = false; }
        }

        partial void OnSearchTextChanged(string value)
        {
            FilteredGames.Clear();
            var filtered = string.IsNullOrWhiteSpace(value) ? Games : Games.Where(g => g.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
            foreach (var game in filtered) FilteredGames.Add(game);
        }

        [RelayCommand] private void SelectGame(GameModel game) { if (game != null) OnGameSelected?.Invoke(game); }
    }
}