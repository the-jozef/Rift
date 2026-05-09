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
        public async Task LoadSteamDataAsync()
        {
            PlayerInfo? playerInfo = null;

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
                    try { ViewNavigator.Instance?.ShowMain(playerInfo, SessionManager.LastLocation); }
                    catch { }
                });
            }
        }
    }
}