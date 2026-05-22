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
using Rift_App.Services;

namespace Rift_App.TitleBars
{
    public partial class SearchBar : UserControl
    {
        public SearchBarViewModel ViewModel { get; } = new();

        public SearchBar()
        {
            InitializeComponent();
            DataContext = ViewModel;

            // Debounced search — fires on every keystroke
            SearchBox.TextChanged += async (_, _) =>
                await ViewModel.OnSearchTextChangedAsync(SearchBox.Text);

            // Wishlist count loaded independently of WishlistViewModel
            Loaded += async (_, _) =>
                ViewModel.WishlistCount = await WishlistCountCache.GetAsync();
        }
    }
}