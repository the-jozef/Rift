using Rift_App.Languages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Rift_App.Services
{
    public static class SteamStartupService
    {
        private static bool _firstRun = !File.Exists(AppPaths.InitFlag);

        public static async Task<bool> CheckAndStartSteamAsync()
        {
            var txtPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steam_appid.txt");
            Debug.WriteLine($"[Startup] steam_appid.txt exists: {File.Exists(txtPath)} at {txtPath}");
            Debug.WriteLine($"[Startup] Contents: '{(File.Exists(txtPath) ? File.ReadAllText(txtPath).Trim() : "MISSING")}'");
            Debug.WriteLine($"[Startup] CWD: {Directory.GetCurrentDirectory()}");

            var dst = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
            if (!File.Exists(dst) && File.Exists(txtPath))
                File.Copy(txtPath, dst);

            if (!SteamworksService.IsSteamInstalled())
            {
                MessageBox.Show(
    L.Get("msg_steam_not_installed"),
    L.Get("msg_steam_not_installed_title"),
    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return false;
            }

            if (_firstRun)
            {
                var result = MessageBox.Show(
    L.Get("msg_welcome"),
    L.Get("msg_welcome_title"),
    MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (result == MessageBoxResult.Cancel)
                {
                    Application.Current.Shutdown();
                    return false;
                }

                MarkFirstRunComplete();
            }

            if (!SteamworksService.IsSteamRunning())
            {
                MessageBox.Show(
    L.Get("msg_steam_not_running"),
    L.Get("msg_steam_launching_title"),
    MessageBoxButton.OK, MessageBoxImage.Information);

                bool launched = await SteamworksService.LaunchSteamAndWaitAsync();
                if (!launched)
                {
                    MessageBox.Show(
    L.Get("msg_steam_launch_failed"),
    L.Get("msg_steam_launch_failed_title"),
    MessageBoxButton.OK, MessageBoxImage.Warning);
                    Application.Current.Shutdown();
                    return false;
                }
            }

            bool init = false;
            for (int attempt = 1; attempt <= 100; attempt++)
            {
                init = SteamworksService.Initialize();
                if (init) break;
                await Task.Delay(5000);
            }

            if (!init)
            {
                MessageBox.Show(
     L.Get("msg_steam_connect_failed"),
     L.Get("msg_steam_connect_failed_title"),
     MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return false;
            }

            return true;
        }

        private static void MarkFirstRunComplete()
        {
            try
            {
                AppPaths.Ensure(AppPaths.Root);
                File.WriteAllText(AppPaths.InitFlag, "1");
                _firstRun = false;
            }
            catch { }
        }
    }
}