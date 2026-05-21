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
                    "Steam is not installed on this computer.\n\n" +
                    "Rift requires Steam to be installed to work.\n" +
                    "Please install Steam from https://store.steampowered.com/about/",
                    "Steam Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return false;
            }

            if (_firstRun)
            {
                var result = MessageBox.Show(
                    "Welcome to Rift!\n\n" +
                    "Rift requires Steam to collect your game library and achievements.\n\n" +
                    "Steam will be launched automatically when needed.\n" +
                    "Click OK to continue.",
                    "Steam Required", MessageBoxButton.OKCancel, MessageBoxImage.Information);

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
                    "Steam is not running.\n\nRift will launch Steam automatically.",
                    "Launching Steam", MessageBoxButton.OK, MessageBoxImage.Information);

                bool launched = await SteamworksService.LaunchSteamAndWaitAsync();
                if (!launched)
                {
                    MessageBox.Show(
                        "Could not launch Steam.\n\nPlease start Steam manually and try again.",
                        "Steam Launch Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    "Could not connect to Steam after several attempts.\n\n" +
                    "Possible causes:\n" +
                    "• Steam is still updating — wait for it to finish\n" +
                    "• You are not logged into Steam\n" +
                    "• steam_appid.txt is missing from the app folder\n\n" +
                    "Please resolve the issue and restart Rift.",
                    "Steam Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
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