using Rift_App.Loading;
using Rift_App.Services;
using Rift_App.ViewModels;
using System.Diagnostics;
using System.Windows;

namespace Rift_App
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
          
            // 1. Check Steam installed + running + initialize Steamworks
            //    Shows MessageBox if Steam is missing or fails to start
            bool steamReady = await SteamStartupService.CheckAndStartSteamAsync();
            if (!steamReady) return; // Shutdown already called inside

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

                // Shutdown Steamworks before closing Steam
                SteamworksService.Shutdown();

                // Close Steam only if Rift launched it
               // SteamworksService.CloseSteamIfWeLaunchedIt();
            }
            catch { }

            base.OnExit(e);
        }
    }
}