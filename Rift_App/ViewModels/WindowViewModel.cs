using CommunityToolkit.Mvvm.Input;
using Rift_App.Library;
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
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public WindowStateViewModel WindowState { get; }

        private bool _showSearchBar;
        public bool ShowSearchBar
        {
            get => _showSearchBar;
            set { _showSearchBar = value; OnPropertyChanged(); }
        }

        private UserControl _currentView;
        public UserControl CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public ICommand StoreCommand { get; }
        public ICommand LibraryCommand { get; }
        public ICommand GamePageCommand { get; }

        public WindowViewModel()
        {
            WindowState = new WindowStateViewModel();

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

            CurrentView = new Store.Store();
            ShowSearchBar = true;
        }
    }
}