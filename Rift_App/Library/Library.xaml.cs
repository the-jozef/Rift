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
        private readonly LibraryViewModel _viewModel = new();

        public Library()
        {
            InitializeComponent();
            DataContext = _viewModel;

            // Wire game selection event to the right panel — no logic here
            _viewModel.OnGameSelected += game => GameDetailPanel.LoadGame(game);

            Loaded += Library_Loaded;
        }

        private async void Library_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsLoggedIn) return;
            await _viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
    }
}