using Rift_App.Database;
using Rift_App.Services;
using System.Windows;

namespace Rift_App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new DatabaseService().Initialize();

            string? lastSteamId = SessionService.Load();
            if (lastSteamId != null)
            {
                var auth = new AuthService();
                bool exists = auth.LoginWithSteam(
                    lastSteamId, out string username, out string _);

                if (exists)
                {
                    MessageBox.Show($"Automaticky prihlaseny: {username}");
                    // TODO: otvor MainWindow
                }
                else
                {
                    SessionService.Clear();
                }
            }
        }
    }
}
