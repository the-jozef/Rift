using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Rift_App.Models;
using Rift_App.Services;
using System.Windows;

namespace Rift_App.ViewModels
{
    public partial class LoadingViewModel : ObservableObject
    {
        // ─── MODE 1: Startup — volá App.xaml.cs ──────────────────────────
        // Called by App.xaml.cs on startup

        public async Task StartupAsync()
        {
            try
            {
                await ApiService.InitDeviceAsync();

                var session = await ApiService.GetSessionAsync();

                if (session != null && session.HasSession)
                {
                    SessionManager.SetSession(
                        session.UserId!.Value,
                        session.Username!,
                        session.SteamId64!,
                        session.LastLocation);

                    await LoadSteamDataAsync();
                }
                else
                {
                    ViewNavigator.Instance?.ShowAuth();
                }
            }
            catch
            {
                ViewNavigator.Instance?.ShowAuth();
            }
        }

        // ─── MODE 2: Po prihlásení — volá ViewNavigator ───────────────────
        // Called by ViewNavigator after login/register

        public async Task LoadSteamDataAsync()
        {
            PlayerInfo? playerInfo = null;
            var library = new List<GameModel>();
            var wishlist = new List<GameModel>();

            try
            {
                var steamId = SessionManager.SteamId64;

                if (!string.IsNullOrEmpty(steamId))
                {
                    var p = ApiService.GetPlayerInfoAsync(steamId);
                    var l = ApiService.GetLibraryAsync(steamId);
                    var w = ApiService.GetWishlistAsync(steamId);

                    await Task.WhenAll(p, l, w);

                    playerInfo = p.Result;
                    library = l.Result;
                    wishlist = w.Result;

                    // Ulož avatar URL do SessionManager — store avatar URL
                    if (playerInfo != null)
                        SessionManager.SetAvatar(playerInfo.AvatarUrl);
                }
            }
            catch { }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        ViewNavigator.Instance?.ShowMain(
                            playerInfo, library, wishlist,
                            SessionManager.LastLocation);
                    }
                    catch { }
                });
            }
        }
    }
}