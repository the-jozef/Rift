using Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App.StoreGamePage
{
    public partial class GamePage : UserControl
    {
        private GameModel? _currentGame;

        public GamePage() => InitializeComponent();

        // Called by MainViewModel
        public void LoadGame(GameModel game)
        {
            _currentGame = game;
            DataContext = game;
        }

        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGame == null) return;

            var result = MessageBox.Show(
                $"How would you like to open {_currentGame.Name}?",
                "Open in...", MessageBoxButton.YesNoCancel);

            if (result == MessageBoxResult.Yes) OpenUrl(_currentGame.SteamStoreUrl);
            else if (result == MessageBoxResult.No) OpenUrl($"steam://store/{_currentGame.AppId}");
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGame != null) OpenUrl($"steam://rungameid/{_currentGame.AppId}");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.ViewModel.ShowStore();
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { }
        }
    }
}