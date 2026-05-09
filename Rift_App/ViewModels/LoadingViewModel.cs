using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Rift_App.Models;
using Rift_App.Services;
using System.Windows;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Rift_App.ViewModels
{
    public partial class LoadingViewModel : ObservableObject
    {
        // ─── MODE 1: Startup ──────────────────────────────────────────────

        public async Task StartupAsync()
        {
            try
            {
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
                    // ── Player info — cache first ──────────────────────────
                    var cacheKey = string.Format(LocalCacheService.KeyPlayer, steamId);
                    playerInfo = await LocalCacheService.LoadAsync<PlayerInfo>(
                        cacheKey, LocalCacheService.AccountTTL);

                    if (playerInfo == null)
                    {
                        playerInfo = await ApiService.GetPlayerInfoAsync(steamId);
                        if (playerInfo != null)
                            await LocalCacheService.SaveAsync(cacheKey, playerInfo);
                    }

                    // ── Library — cache first ──────────────────────────────
                    var libKey = string.Format(LocalCacheService.KeyLibrary, steamId);
                    library = await LocalCacheService.LoadAsync<List<GameModel>>(
                        libKey, LocalCacheService.LibraryTTL) ?? new List<GameModel>();

                    if (library.Count == 0)
                    {
                        library = await ApiService.GetLibraryAsync(steamId);
                        if (library.Count > 0)
                            await LocalCacheService.SaveAsync(libKey, library);
                    }

                    // ── Wishlist — skontroluj WishlistCacheService ─────────
                    // Ak cache existuje → okamžité, inak spusti preload v pozadí
                    var wishlistCached = await WishlistCacheService.LoadAsync(steamId);

                    if (wishlistCached == null || wishlistCached.Count == 0)
                    {
                        // Prvé spustenie — spusti detailný fetch v pozadí
                        // WishlistViewModel si ho vyzdvihne z cache keď dobeží
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                Debug.WriteLine("[Loading] Wishlist preload started...");
                                var detailed = await ApiService.GetWishlistDetailedAsync(steamId);
                                if (detailed != null && detailed.Count > 0)
                                {
                                    await WishlistCacheService.SaveAsync(steamId, detailed);
                                    Debug.WriteLine($"[Loading] Wishlist preload done: {detailed.Count} games");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Loading] Wishlist preload error: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"[Loading] Wishlist cache exists: {wishlistCached.Count} games");
                    }

                    // ── Avatar ────────────────────────────────────────────
                    if (playerInfo != null)
                        SessionManager.SetAvatar(playerInfo.AvatarUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Loading] LoadSteamData error: {ex.Message}");
            }
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