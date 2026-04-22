using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rift_App.Login_Register;
using System;

namespace Rift_App.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty] private object currentView;
        [ObservableProperty] private object accountSelectionView;

        private Login_loading _preloadedLoading;

        // Tvoje flags (ak ich chceš neskôr použiť na ešte presnejší control)
        private bool _loadingReady = false;
        private bool _loadingRequested = false;

        public RegisterViewModel()
        {
            CurrentView = new AccountSelection();

            // === PRELOAD === 
            _preloadedLoading = new Login_loading();
            _preloadedLoading.VideoReady += PreloadedLoading_VideoReady;
            _preloadedLoading.PreloadVideo();   // ← video sa začne načítavať už teraz
        }

        private void PreloadedLoading_VideoReady(object? sender, EventArgs e)
        {
            _loadingReady = true;

            // Ak niekto stlačil Loading ešte pred dokončením preloadu, prepneme teraz
            if (_loadingRequested)
            {
                CurrentView = _preloadedLoading;
                _loadingRequested = false;
            }
        }

        [RelayCommand]
        public void ShowLogin() => CurrentView = new Login();

        [RelayCommand]
        public void ShowSteamConnection() => CurrentView = new SteamConnection();

        [RelayCommand]
        private void ShowRegister() => CurrentView = new Register();

        [RelayCommand]
        private void AccountSelection() => CurrentView = null;

        [RelayCommand]
        private void Loading()
        {
            if (_loadingReady)
            {
                // Video je už pripravené → ihneď zobrazíme s videom od začiatku
                CurrentView = _preloadedLoading;
            }
            else
            {
                // Ešte sa nenačítalo (veľmi nepravdepodobné, ak je ViewModel vytvorený na začiatku)
                _loadingRequested = true;
                // Môžeš tu nechať aktuálny view (user uvidí, že sa nič nedeje 0,1s)
                // alebo rovno prepnúť (video sa spustí hneď ako bude ready)
                CurrentView = _preloadedLoading;
            }
        }
    }
}