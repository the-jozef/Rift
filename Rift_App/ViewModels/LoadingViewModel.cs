using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Rift_App.Models;
using Rift_App.Services;
using System.Windows;
using System.Threading.Tasks;

namespace Rift_App.ViewModels
{
    public partial class LoadingViewModel : ObservableObject
    {
        // ─── MODE 1: Startup ──────────────────────────────────────────────

        public async Task StartupAsync()
        {
            try
            {
                // Inicializuj tag slovník na pozadí — initialize tag dictionary in background
                _ = TagService.InitAsync();

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

        // ─── MODE 2: Po prihlásení ────────────────────────────────────────

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
                    // Skús player info z cache — try player info from cache
                    var cacheKey = string.Format(LocalCacheService.KeyPlayer, steamId);
                    playerInfo = await LocalCacheService.LoadAsync<PlayerInfo>(
                        cacheKey, LocalCacheService.AccountTTL);

                    if (playerInfo == null)
                    {
                        playerInfo = await ApiService.GetPlayerInfoAsync(steamId);
                        if (playerInfo != null)
                            await LocalCacheService.SaveAsync(cacheKey, playerInfo);
                    }

                    // Library — cache + live
                    var libKey = string.Format(LocalCacheService.KeyLibrary, steamId);
                    library = await LocalCacheService.LoadAsync<List<GameModel>>(
                        libKey, LocalCacheService.LibraryTTL) ?? new List<GameModel>();

                    if (library.Count == 0)
                    {
                        library = await ApiService.GetLibraryAsync(steamId);
                        if (library.Count > 0)
                            await LocalCacheService.SaveAsync(libKey, library);
                    }

                    // Wishlist — cache + live
                    var wishKey = string.Format(LocalCacheService.KeyWishlist, steamId);
                    wishlist = await LocalCacheService.LoadAsync<List<GameModel>>(
                        wishKey, LocalCacheService.WishlistTTL) ?? new List<GameModel>();

                    if (wishlist.Count == 0)
                    {
                        wishlist = await ApiService.GetWishlistAsync(steamId);
                        if (wishlist.Count > 0)
                            await LocalCacheService.SaveAsync(wishKey, wishlist);
                    }

                    // Ulož avatar — save avatar
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