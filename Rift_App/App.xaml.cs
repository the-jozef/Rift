using Rift_App.Database;
using System.Windows;
namespace Rift_App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new DatabaseService().Initialize();
        }
    }
}
