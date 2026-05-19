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
using Rift_App.ViewModels;

namespace Rift_App.TitleBars
{
    public partial class SearchBar : UserControl
    {
        public SearchBarViewModel ViewModel { get; } = new();

        public SearchBar()
        {
            InitializeComponent();
            DataContext = ViewModel;

            // Wire up text changed for debounced search
            SearchBox.TextChanged += async (_, _) =>
                await ViewModel.OnSearchTextChangedAsync(SearchBox.Text);

            // Close dropdown when clicking outside
            SearchBox.LostFocus += (_, _) =>
            {
                // Small delay so button clicks in dropdown still register
                System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    Dispatcher.Invoke(() => ViewModel.CloseDropdown()));
            };

            // Refresh wishlist count when loaded
            Loaded += async (_, _) =>
                await ViewModel.RefreshWishlistCountAsync();
        }
    }
}