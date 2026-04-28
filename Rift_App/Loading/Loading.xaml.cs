using Rift_App.Models;
using Rift_App.Services;
using Rift_App.ViewModels;
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
using System.Windows.Shapes;

namespace Rift_App.Loading
{
    public partial class LoadingWindow : Window
    {
        public LoadingViewModel ViewModel { get; } = new LoadingViewModel();

        public LoadingWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;
            Closing += (s, e) => { e.Cancel = true; Hide(); };
        }

        // Called by App.xaml.cs on startup
        public void StartStartup() => _ = ViewModel.StartupAsync();

        // Called by ViewNavigator after login/register
        public void StartLoading() => _ = ViewModel.LoadSteamDataAsync();
    }
}