using Rift_App.Models;
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

namespace Rift_App.Store
{
    public partial class Store : UserControl
    {
        private readonly StoreViewModel _viewModel = new();

        public Store()
        {
            InitializeComponent();
            DataContext = _viewModel;

            _viewModel.OnGameSelected += async game =>
            {
                var full = await ApiService.GetGameDetailsAsync(game.AppId);
                if (Application.Current.MainWindow is MainWindow main)
                    main.ViewModel.ShowGamePageCommand.Execute(full ?? game);
            };

            Loaded += async (_, _) => await _viewModel.LoadStoreCommand.ExecuteAsync(null);
        }
    }
}