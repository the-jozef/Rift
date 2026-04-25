using CommunityToolkit.Mvvm.Input;
using Rift_App.ac

using Rift_App.Account;
using Rift_App.Authorization;
using Rift_App.GameModels;
using Rift_App.Models;
using Rift_App.Models.Rift_App.Models;
using Rift_App.Services;
using Rift_App.Store_Controls;
using Rift_App.Testing;
using Rift_App.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Closing = hide, not exit
            this.Closing += (s, e) =>
            {
                e.Cancel = true;
                this.Hide();
            };
        }

        // ─── INITIALIZE DATA ──────────────────────────────────────────────
        /// Navigates to last known location.
        public void InitializeData(
            PlayerInfo? playerInfo,
            List<GameModel> library,
            List<GameModel> wishlist,
            string lastLocation)
        {
            try
            {
                NavigateToLocation(lastLocation);
            }
            catch
            {
                NavigateToLocation("Store");
            }
        }

        // ─── LOCATION NAVIGATION ──────────────────────────────────────────

        private void NavigateToLocation(string location)
        {
            try
            {
                switch (location)
                {
                    case "Library": ShowLibrary(); break;
                    case "Wishlist": ShowWishlist(); break;
                    case "Account": ShowAccount(); break;
                    default: ShowStore(); break;
                }
            }
            catch { ShowStore(); }
        }

        // ─── PAGE METHODS — called from MenuBar and code ──────────────────

        public void ShowStore()
        {
            try
            {
                _ = ApiService.SaveSessionAsync("Store");
                Content = new Store();
            }
            catch { }
        }

        public void ShowLibrary()
        {
            try
            {
                _ = ApiService.SaveSessionAsync("Library");
                Content = new Library.Library();
            }
            catch { }
        }

        public void ShowWishlist()
        {
            try
            {
                _ = ApiService.SaveSessionAsync("Wishlist");
                Content = new Wishlist.Wishlist();
            }
            catch { }
        }

        public void ShowAccount()
        {
            try
            {
                _ = ApiService.SaveSessionAsync("Account");
                Content = new Acconu();
            }
            catch { }
        }
        /// Opens game detail page — called from Store, Library, Wishlist.
        public void ShowGamePage(GameModel game)
        {
            try
            {
                var gamePage = new StoreGamePage.GamePage();
                gamePage.LoadGame(game);
                Content = gamePage;
            }
            catch { }
        }

        // ─── SWITCH ACCOUNT ───────────────────────────────────────────────

        public void SwitchAccount()
        {
            try
            {
                SessionManager.Clear();
                ViewNavigator.Instance?.SwitchAccount();
            }
            catch { }
        }

        // ─── GUEST MODE ───────────────────────────────────────────────────
        public bool IsGuest => !SessionManager.IsLoggedIn;
    }
}