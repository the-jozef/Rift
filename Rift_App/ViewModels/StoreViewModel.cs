using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Rift_App.Image;
using System.Collections.ObjectModel;
using Rift_App.Services;
using Rift_App.Models;

namespace Rift_App.ViewModels
{
    public partial class StoreViewModel : ObservableObject
    {
        public ObservableCollection<GameModel> NewTrending { get; } = new();
        public ObservableCollection<GameModel> TopSellers { get; } = new();
        public ObservableCollection<GameModel> Specials { get; } = new();

        private int _newTrendingPage = 0, _topSellersPage = 0, _specialsPage = 0;

        [ObservableProperty] private bool _isLoadingNewTrending = false;
        [ObservableProperty] private bool _isLoadingTopSellers = false;
        [ObservableProperty] private bool _isLoadingSpecials = false;
        [ObservableProperty] private bool _hasMoreNewTrending = true;
        [ObservableProperty] private bool _hasMoreTopSellers = true;
        [ObservableProperty] private bool _hasMoreSpecials = true;

        public event Action<GameModel>? OnGameSelected;

        [RelayCommand]
        public async Task LoadStoreAsync()
        {
            await Task.WhenAll(LoadNewTrendingAsync(), LoadTopSellersAsync(), LoadSpecialsAsync());
            await ApiService.SaveSessionAsync("Store");
        }

        private async Task LoadNewTrendingAsync()
        {
            IsLoadingNewTrending = true;
            try { var g = await ApiService.GetNewTrendingAsync(_newTrendingPage); foreach (var game in g) NewTrending.Add(game); if (g.Count < 10) HasMoreNewTrending = false; }
            catch { }
            finally { IsLoadingNewTrending = false; }
        }

        [RelayCommand] private async Task ShowMoreNewTrendingAsync() { _newTrendingPage++; await LoadNewTrendingAsync(); }

        private async Task LoadTopSellersAsync()
        {
            IsLoadingTopSellers = true;
            try { var g = await ApiService.GetTopSellersAsync(_topSellersPage); foreach (var game in g) TopSellers.Add(game); if (g.Count < 10) HasMoreTopSellers = false; }
            catch { }
            finally { IsLoadingTopSellers = false; }
        }

        [RelayCommand] private async Task ShowMoreTopSellersAsync() { _topSellersPage++; await LoadTopSellersAsync(); }

        private async Task LoadSpecialsAsync()
        {
            IsLoadingSpecials = true;
            try { var g = await ApiService.GetSpecialsAsync(_specialsPage); foreach (var game in g) Specials.Add(game); if (g.Count < 10) HasMoreSpecials = false; }
            catch { }
            finally { IsLoadingSpecials = false; }
        }

        [RelayCommand] private async Task ShowMoreSpecialsAsync() { _specialsPage++; await LoadSpecialsAsync(); }
        [RelayCommand] private void SelectGame(GameModel game) { if (game != null) OnGameSelected?.Invoke(game); }
    }
}
