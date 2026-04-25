using CommunityToolkit.Mvvm.Input;
using Rift_App.Library;
using Rift_App.Authorization;
using Rift_App.Store;
using Rift_App.StoreGamePage;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rift_App.ViewModels
{
    public class WindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Sub-ViewModels
        public WindowStateViewModel WindowState { get; }
        public StoreViewModel Store { get; }

        // Current page shown in MainWindow
        // Aktuálna stránka zobrazená v MainWindow
        private object _currentView = new object();
        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        private bool _showSearchBar;
        public bool ShowSearchBar
        {
            get => _showSearchBar;
            set { _showSearchBar = value; OnPropertyChanged(); }
        }

        // ─── NAVIGATION COMMANDS ──────────────────────────────────────────

        public ICommand StoreCommand { get; }
        public ICommand LibraryCommand { get; }
        public ICommand GamePageCommand { get; }
        public ICommand SwitchAccountCommand { get; }

        public WindowViewModel()
        {
            WindowState = new WindowStateViewModel();
            Store = new StoreViewModel();

            StoreCommand = new RelayCommand(() =>
            {
                CurrentView = new Store.Store();
                ShowSearchBar = true;
            });

            LibraryCommand = new RelayCommand(() =>
            {
                CurrentView = new Library.Library();
                ShowSearchBar = false;
            });

            GamePageCommand = new RelayCommand(() =>
            {
                CurrentView = new GamePage();
                ShowSearchBar = false;
            });

            // FIXED: Use ViewNavigator.Instance instead of new ViewNavigator()
            // Používame ViewNavigator.Instance namiesto new ViewNavigator()
            SwitchAccountCommand = new RelayCommand(() =>
            {
                try
                {
                    Services.SessionManager.Clear();
                    ViewNavigator.Instance?.SwitchAccount();
                }
                catch { }
            });

            // Default page
            CurrentView = new Store.Store();
            ShowSearchBar = true;
        }
    }
}