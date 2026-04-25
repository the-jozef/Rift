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

namespace Rift_App.ViewModels
{
    public partial class WishlistViewModel : ObservableObject
    {
        public ObservableCollection<GameModel> Games { get; } = new();

        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private int _totalGames = 0;
        [ObservableProperty] private bool _isEmpty = false;

        public event Action<GameModel>? OnGameSelected;

        [RelayCommand]
        public async Task LoadWishlistAsync()
        {
            IsLoading = true; Games.Clear(); IsEmpty = false;
            try
            {
                var games = await ApiService.GetWishlistAsync(SessionManager.SteamId64);
                if (games == null || games.Count == 0) { IsEmpty = true; TotalGames = 0; return; }
                foreach (var game in games) Games.Add(game);
                TotalGames = Games.Count;
                await ApiService.SaveSessionAsync("Wishlist");
            }
            catch { }
            finally { IsLoading = false; }
        }

        [RelayCommand] private void SelectGame(GameModel game) { if (game != null) OnGameSelected?.Invoke(game); }
    }
}
