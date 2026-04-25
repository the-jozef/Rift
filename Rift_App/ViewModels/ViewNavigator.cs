using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Rift_App.Authorization;
using Rift_App.Loading;
using Rift_App.Models;
using Rift_App.Models.Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rift_App.ViewModels
{
    public class ViewNavigator
    {
        // ─── SINGLETON ────────────────────────────────────────────────────

        public static ViewNavigator? Instance { get; private set; }

        public static void Initialize(LoadingWindow loadingWindow, AuthWindow authWindow, MainWindow mainWindow)
        {
            Instance = new ViewNavigator(loadingWindow, authWindow, mainWindow);
        }

        private readonly LoadingWindow _loadingWindow;
        private readonly AuthWindow _authWindow;
        private readonly MainWindow _mainWindow;

        private ViewNavigator(LoadingWindow l, AuthWindow a, MainWindow m)
        {
            _loadingWindow = l;
            _authWindow = a;
            _mainWindow = m;
        }

        // ─── LOADING ──────────────────────────────────────────────────────
        public void ShowLoading()
        {
            try
            {
                _mainWindow.Hide();
                _authWindow.Hide();
                _loadingWindow.Show();
            }
            catch { }
        }

        // ─── AUTH WINDOW ──────────────────────────────────────────────────
        public void ShowAccountSelection()
        {
            try
            {
                _mainWindow.Hide();
                _loadingWindow.Hide();
                _authWindow.ShowView(new AccountSelection());
                _authWindow.Show();
            }
            catch { }
        }

        public void ShowLogin()
        {
            try
            {
                _authWindow.ShowView(new Login());
                _authWindow.Show();
            }
            catch { }
        }

        public void ShowSteamConnection()
        {
            try
            {
                _authWindow.ShowView(new SteamConnection());
            }
            catch { }
        }

        public void ShowRegister(string steamId, string steamName)
        {
            try
            {
                var register = new Register();
                register.PreFill(steamId, steamName);
                _authWindow.ShowView(register);
            }
            catch { }
        }

        // ─── MAIN WINDOW ──────────────────────────────────────────────────
        public void ShowMainWindow(
            PlayerInfo? playerInfo,
            List<GameModel> library,
            List<GameModel> wishlist,
            string lastLocation)
        {
            try
            {
                _loadingWindow.Hide();
                _authWindow.Hide();
                _mainWindow.InitializeData(playerInfo, library, wishlist, lastLocation);
                _mainWindow.Show();
            }
            catch { }
        }

        public void SwitchAccount()
        {
            try
            {
                _mainWindow.Hide();
                _authWindow.ShowView(new AccountSelection());
                _authWindow.Show();
            }
            catch { }
        }
    }
}