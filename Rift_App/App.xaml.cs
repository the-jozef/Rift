using Rift_App.Loading;
using Rift_App.Services;
using Rift_App.ViewModels;
using System.Windows;

namespace Rift_App
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var lang = LanguageService.LoadLanguage();
            LanguageService.Switch(lang);
            
            // 1. Check Steam installed + running + initialize Steamworks
            bool steamReady = await SteamStartupService.CheckAndStartSteamAsync();
            if (!steamReady) return;  // Shutdown already called inside

            // 2. Initialize tag dictionary in background
            _ = TagService.InitAsync();

            // 3. Create windows and start normal app flow
            var auth = new AuthWindow();
            var loading = new LoadingWindow();
            var main = new MainWindow();

            ViewNavigator.Initialize(auth, loading, main);

            loading.Show();
            loading.StartStartup();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                SteamAuthService.Cancel();

                // Stop Steam timer + unregister callbacks via ViewModel
                ViewNavigator.Instance?.MainViewModel?.Cleanup();


                // Shutdown Steamworks before closing Steam
                SteamworksService.Shutdown();

                // Close Steam only if Rift launched it
                //SteamworksService.CloseSteamIfWeLaunchedIt();
            }
            catch { }

            base.OnExit(e);
        }
    }
}