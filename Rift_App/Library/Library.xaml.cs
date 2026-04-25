using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rift_App.Models;
using Rift_App.Services;
using Rift_App.ViewModels;

namespace Rift_App.Library
{
    public partial class Library : UserControl
    {
        private readonly LibraryViewModel _viewModel;

        public Library()
        {
            InitializeComponent();

            _viewModel = new LibraryViewModel();
            DataContext = _viewModel;

            // When game clicked → open game page
            _viewModel.OnGameSelected += HandleGameSelected;

            Loaded += async (s, e) =>
            {
                try
                {
                    // Guest mode — show message, don't load
                    // Hosť — zobrazíme správu, nenačítame nič
                    if (!SessionManager.IsLoggedIn)
                    {
                        ShowGuestMessage();
                        return;
                    }

                    await _viewModel.LoadLibraryCommand.ExecuteAsync(null);
                }
                catch { }
            };
        }

        // ─── GAME SELECTED ────────────────────────────────────────────────

        private void HandleGameSelected(GameModel game)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowGamePage(game);
            }
            catch { }
        }

        // ─── GUEST MESSAGE ────────────────────────────────────────────────

        private void ShowGuestMessage()
        {
            try
            {
                // Show guest panel, hide games list
                // Zobrazíme hosťovský panel, skryjeme zoznam hier
                if (GuestPanel != null) GuestPanel.Visibility = Visibility.Visible;
                if (GamesPanel != null) GamesPanel.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        // ─── GAME CLICK ───────────────────────────────────────────────────

        private void GameItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is GameModel game)
                    _viewModel.SelectGameCommand.Execute(game);
            }
            catch { }
        }

        // ─── SEARCH BOX ───────────────────────────────────────────────────
        // Bound via TextChanged in XAML

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox tb)
                    _viewModel.SearchText = tb.Text;
            }
            catch { }
        }
    }
}