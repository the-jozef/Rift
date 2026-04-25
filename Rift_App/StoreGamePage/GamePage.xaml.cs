using System;
using System.Collections.Generic;
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
using Rift_App.Models;
using System.Diagnostics;

namespace Rift_App.StoreGamePage
{
    public partial class GamePage : UserControl
    {
        private GameModel? _currentGame;

        public GamePage()
        {
            InitializeComponent();
        }

        // ─── LOAD GAME DATA ───────────────────────────────────────────────

        /// <summary>
        /// Called by MainWindow.ShowGamePage() with selected game.
        /// Volané z MainWindow.ShowGamePage() s vybranou hrou.
        /// </summary>
        public void LoadGame(GameModel game)
        {
            try
            {
                _currentGame = game;
                DataContext = game;
            }
            catch { }
        }

        // ─── BUY BUTTON ───────────────────────────────────────────────────

        /// <summary>
        /// Buy button — asks user: open in Browser or Steam app?
        /// Tlačidlo kúpiť — pýta sa: otvoriť v prehliadači alebo Steam appke?
        /// </summary>
        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentGame == null) return;

                var result = MessageBox.Show(
                    $"How would you like to open {_currentGame.Name}?",
                    "Open in...",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                // Yes = Browser, No = Steam App
                if (result == MessageBoxResult.Yes)
                {
                    // Open in browser — otvoríme v prehliadači
                    OpenUrl(_currentGame.SteamStoreUrl);
                }
                else if (result == MessageBoxResult.No)
                {
                    // Open in Steam app — otvoríme v Steam appke
                    OpenUrl($"steam://store/{_currentGame.AppId}");
                }
                // Cancel = do nothing
            }
            catch { }
        }

        // ─── PLAY BUTTON ──────────────────────────────────────────────────

        /// <summary>
        /// Play button — launches game directly via Steam protocol.
        /// Tlačidlo hrať — spustí hru priamo cez Steam protokol.
        /// </summary>
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentGame == null) return;

                // steam://rungameid/APPID launches the game directly
                OpenUrl($"steam://rungameid/{_currentGame.AppId}");
            }
            catch { }
        }

        // ─── BACK BUTTON ──────────────────────────────────────────────────

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowStore();
            }
            catch { }
        }

        // ─── HELPER ───────────────────────────────────────────────────────

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}