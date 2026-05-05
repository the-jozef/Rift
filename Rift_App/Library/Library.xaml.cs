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
            Loaded += Library_Loaded;
        }

        private async void Library_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsLoggedIn)
            {
                return;
            }
            await _viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
        private void GameItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameModel game)
                _viewModel.SelectGameCommand.Execute(game);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
                _viewModel.SearchText = tb.Text;
        }
    }
}