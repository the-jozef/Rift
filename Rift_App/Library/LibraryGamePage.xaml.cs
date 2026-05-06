using System;
using System.Collections.Generic;
using System.Text;
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
using Rift_App.ViewModels;

namespace Rift_App.Library
{
    public partial class LibraryGamePage : UserControl
    {
        public LibraryGameViewModel ViewModel { get; } = new();

        public LibraryGamePage()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public void LoadGame(GameModel game) =>
            _ = ViewModel.LoadAsync(game);
    }
}