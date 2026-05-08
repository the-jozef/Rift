using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Rift_App.Services
{
    /// <summary>
    /// Handles Steam startup checks shown at app launch.
    /// Shows MessageBox if Steam is not installed.
    /// Launches Steam automatically if not running.
    /// </summary>
    public static class SteamStartupService
    {
        private static bool _firstRun = !File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "initialized.flag"));

        // ─── MAIN ENTRY POINT ─────────────────────────────────────────────

        /// <summary>
        /// Call this at app startup (before showing any window).
        /// Returns false if the app should shut down (Steam not installed).
        /// </summary>
        public static async Task<bool> CheckAndStartSteamAsync()
        {
            // ── DEBUG: verify steam_appid.txt ──────────────────────────────────
            var txtPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steam_appid.txt");
            Debug.WriteLine($"[Startup] steam_appid.txt exists: {File.Exists(txtPath)} at {txtPath}");
            Debug.WriteLine($"[Startup] Contents: '{(File.Exists(txtPath) ? File.ReadAllText(txtPath).Trim() : "MISSING")}'");
            Debug.WriteLine($"[Startup] CWD: {Directory.GetCurrentDirectory()}");

            // Fix: make sure steam_appid.txt is also in the current working directory
           var dst = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
            if (!File.Exists(dst) && File.Exists(txtPath))
                File.Copy(txtPath, dst);
            // ──────────────────────────────────────────────────────────────────

            // 1. Check if Steam is installed
            if (!SteamworksService.IsSteamInstalled())
            {
                MessageBox.Show(
                    "Steam is not installed on this computer.\n\n" +
                    "Rift requires Steam to be installed to work.\n" +
                    "Please install Steam from https://store.steampowered.com/about/",
                    "Steam Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Application.Current.Shutdown();
                return false;
            }

            // 2. First run — inform user that Steam is required
            if (_firstRun)
            {
                var result = MessageBox.Show(
                    "Welcome to Rift!\n\n" +
                    "Rift requires Steam to collect your game library and achievements.\n\n" +
                    "Steam will be launched automatically when needed.\n" +
                    "Click OK to continue.",
                    "Steam Required",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Cancel)
                {
                    Application.Current.Shutdown();
                    return false;
                }

                MarkFirstRunComplete();
            }

            // 3. Launch Steam if not running
            if (!SteamworksService.IsSteamRunning())
            {
                MessageBox.Show(
                    "Steam is not running.\n\nRift will launch Steam automatically.",
                    "Launching Steam",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                bool launched = await SteamworksService.LaunchSteamAndWaitAsync();
                if (!launched)
                {
                    MessageBox.Show(
                        "Could not launch Steam.\n\nPlease start Steam manually and try again.",
                        "Steam Launch Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    Application.Current.Shutdown();
                    return false;
                }
            }

            // 4. Initialize Steamworks — retry a few times in case Steam is still starting up
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
                    "Steam Connection Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Application.Current.Shutdown();
                return false;
            }

            return true;
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static void MarkFirstRunComplete()
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RiftApp");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "initialized.flag"), "1");
                _firstRun = false;
            }
            catch { }
        }
    }
}