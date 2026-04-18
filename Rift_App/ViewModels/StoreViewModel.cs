using CommunityToolkit.Mvvm.Input;
using Rift_App.GameModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Rift_App.ViewModels
{
    public class StoreViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public List<GameModel> FeaturedGames { get; set; } = new();

        private int _index = 0;

        private GameModel? _currentGame;
        public GameModel? CurrentGame
        {
            get => _currentGame;
            set
            {
                _currentGame = value;
                OnPropertyChanged();
            }
        }

        public ICommand NextCommand => new RelayCommand(() =>
        {
            _index = (_index + 1) % FeaturedGames.Count;
            CurrentGame = FeaturedGames[_index];
        });

        public ICommand PrevCommand => new RelayCommand(() =>
        {
            _index = (_index - 1 + FeaturedGames.Count) % FeaturedGames.Count;
            CurrentGame = FeaturedGames[_index];
        });

        public StoreViewModel()
        {
            FeaturedGames = new List<GameModel>
            {
                new GameModel
                {
                    Title = "R.E.P.O.",
                    Price = "9,49€",
                    Tags = new() { "Stealth", "Online Co-Op", "Co-op" },
                    ReasonText = "Recommended because you played games tagged with"
                },
                new GameModel
                {
                    Title = "Gray Zone Warfare",
                    Price = "26,79€",
                    DiscountPercent = "-33%",
                    OriginalPrice = "39,99€",
                    Tags = new() { "Top Seller" }
                }
            };
            CurrentGame = FeaturedGames[0];
        }
    }
}