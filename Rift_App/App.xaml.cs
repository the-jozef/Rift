using Rift_App.Loading;
using Rift_App.Services;
using System.Windows;
using Rift_App.ViewModels;

namespace Rift_App
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Create all 3 windows
                var loadingWindow = new LoadingWindow();
                var authWindow = new AuthWindow();
                var mainWindow = new MainWindow();

                // Initialize ViewNavigator — singleton
                ViewNavigator.Initialize(loadingWindow, authWindow, mainWindow);

                // Register device on server
                await ApiService.InitDeviceAsync();

                // Check for saved session
                var session = await ApiService.GetSessionAsync();

                if (session != null && session.HasSession)
                {
                    // Known account → set session → show Loading
                    SessionManager.SetSession(
                        session.UserId!.Value,
                        session.Username!,
                        session.SteamId64!,
                        session.LastLocation
                    );
                    ViewNavigator.Instance?.ShowLoading();
                }
                else
                {
                    // No session → AccountSelection
                    ViewNavigator.Instance?.ShowAccountSelection();
                }
            }
            catch
            {
                try { ViewNavigator.Instance?.ShowAccountSelection(); }
                catch { new AuthWindow().Show(); }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { SteamAuthService.Cancel(); }
            catch { }
            base.OnExit(e);
        }
    }
}