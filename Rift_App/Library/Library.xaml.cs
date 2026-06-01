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

            _viewModel.OnGameSelected += game => GameDetailPanel.LoadGame(game);

            Loaded += Library_Loaded;

            Unloaded += (_, _) => GameDetailPanel.ViewModel.Dispose();
        }

        private async void Library_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsLoggedIn) return;
            await _viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
    }
}