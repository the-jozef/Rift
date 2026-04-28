using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Rift_App.Authorization;
using Rift_App.Loading;
using Rift_App.Models;
using Rift_App.ViewModels;
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

        public static void Initialize(AuthWindow auth, LoadingWindow loading, MainWindow main)
        {
            Instance = new ViewNavigator(auth, loading, main);
        }

        private readonly AuthWindow _auth;
        private readonly LoadingWindow _loading;
        private readonly MainWindow _main;

        private ViewNavigator(AuthWindow auth, LoadingWindow loading, MainWindow main)
        {
            _auth = auth;
            _loading = loading;
            _main = main;
        }

        // ─── SHOW AUTH ────────────────────────────────────────────────────

        /// <summary>
        /// Shows AuthWindow at AccountSelection — hides everything else.
        /// </summary>
        public void ShowAuth()
        {
            try
            {
                _main.Hide();
                _loading.Hide();
                _auth.ViewModel.ShowAccountSelection();
                _auth.Show();
            }
            catch { }
        }

        /// <summary>
        /// Called when user clicks "Switch Account" in MainWindow.
        /// </summary>
        public void SwitchToAuth()
        {
            try
            {
                _main.Hide();
                _auth.ViewModel.ShowAccountSelection();
                _auth.Show();
            }
            catch { }
        }

        // ─── SHOW LOADING ─────────────────────────────────────────────────

        /// <summary>
        /// Shows LoadingWindow — hides auth, starts loading Steam data.
        /// Called after successful login or register.
        /// </summary>
        public void ShowLoading()
        {
            try
            {
                _auth.Hide();
                _main.Hide();
                _loading.StartLoading();
                _loading.Show();
            }
            catch { }
        }

        // ─── SHOW MAIN ────────────────────────────────────────────────────

        /// <summary>
        /// Called by LoadingWindow when all data is ready.
        /// Navigates to last known location.
        /// </summary>
        public void ShowMain(PlayerInfo? playerInfo, List<GameModel> library, List<GameModel> wishlist, string lastLocation)
        {
            try
            {
                _loading.Hide();
                _auth.Hide();
                _main.ViewModel.NavigateToLastLocation(lastLocation);
                _main.Show();
            }
            catch { }
        }
    }
}