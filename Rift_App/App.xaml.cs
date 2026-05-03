using Rift_App.Loading;
using Rift_App.Services;
using System.Windows;
using Rift_App.ViewModels;

namespace Rift_App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var auth = new AuthWindow();
            var loading = new LoadingWindow();
            var main = new MainWindow();

            _ = TagService.InitAsync();

            ViewNavigator.Initialize(auth, loading, main);

            loading.Show();
            loading.StartStartup();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { SteamAuthService.Cancel(); }
            catch { }
            base.OnExit(e);
        }
    }
}