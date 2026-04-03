using System.Configuration;
using System.Data;
using System.Windows;

namespace Rift_App
{
    public partial class App : Application
    {
        public static ConfigService Config { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            Config = new ConfigService();   // ← tu sa načíta kľúč
            base.OnStartup(e);
        }
    }
}
