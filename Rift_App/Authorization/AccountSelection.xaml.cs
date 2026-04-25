using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App.Authorization
{
    public partial class AccountSelection : UserControl
    {
        private readonly AccountSelectionViewModel _viewModel;

        public AccountSelection()
        {
            InitializeComponent();

            _viewModel = new AccountSelectionViewModel();
            DataContext = _viewModel;

            // When account selected → go to Loading
            _viewModel.OnAccountSelected += () =>
            {
                try { ViewNavigator.Instance?.ShowLoading(); }
                catch { }
            };

            // No accounts on device → go to Login
            _viewModel.OnGoToLogin += () =>
            {
                try { ViewNavigator.Instance?.ShowLogin(); }
                catch { }
            };

            // Load accounts when page opens
            Loaded += async (s, e) =>
            {
                try { await _viewModel.LoadAccountsCommand.ExecuteAsync(null); }
                catch { }
            };
        }

        // ─── ACCOUNT CLICK ────────────────────────────────────────────────

        private async void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is AccountInfo account)
                    await _viewModel.SelectAccountCommand.ExecuteAsync(account);
            }
            catch { }
        }

        // ─── REMOVE FROM DEVICE ───────────────────────────────────────────

        private async void RemoveFromDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is AccountInfo account)
                {
                    var result = MessageBox.Show(
                        $"Remove {account.Username} from this device?\nYour account will not be deleted.",
                        "Remove Account",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                        await _viewModel.RemoveFromDeviceCommand.ExecuteAsync(account);
                }
            }
            catch { }
        }

        // ─── DELETE ACCOUNT FULLY ─────────────────────────────────────────

        private async void DeleteAccountFully_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.DataContext is AccountInfo account)
                {
                    var result = MessageBox.Show(
                        $"Permanently delete {account.Username}?\nThis cannot be undone.",
                        "Delete Account",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                        await _viewModel.DeleteAccountFullyCommand.ExecuteAsync(account);
                }
            }
            catch { }
        }
    }
}