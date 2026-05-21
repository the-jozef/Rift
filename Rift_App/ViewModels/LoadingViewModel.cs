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
        [ObservableProperty] private string _loadingText = "Loading";

        private System.Threading.CancellationTokenSource? _dotsCts;

        private void StartDotsAnimation()
        {
            _dotsCts?.Cancel();
            _dotsCts = new System.Threading.CancellationTokenSource();
            var token = _dotsCts.Token;

            Task.Run(async () =>
            {
                string[] frames = { "Loading", "Loading .", "Loading ..", "Loading ..." };
                int i = 0;
                while (!token.IsCancellationRequested)
                {
                    var frame = frames[i % frames.Length];
                    Application.Current.Dispatcher.Invoke(() => LoadingText = frame);
                    i++;
                    await Task.Delay(800, token).ContinueWith(_ => { });
                }
            });
        }

        private void StopDotsAnimation()
        {
            _dotsCts?.Cancel();
        }


        // ─── MODE 1: Startup ──────────────────────────────────────────────
        public async Task StartupAsync()
        {
            StartDotsAnimation();
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
                    StopDotsAnimation();
                    ViewNavigator.Instance?.ShowAuth();
                }
            }
            catch
            {
                StopDotsAnimation();
                ViewNavigator.Instance?.ShowAuth();
            }
        }

        public async Task LoadSteamDataAsync()
        {
            StartDotsAnimation();
            PlayerInfo? playerInfo = null;

            var minimumDelay = Task.Delay(TimeSpan.FromSeconds(5));    //Change to 15-20 sec

            try
            {
                var steamId = SessionManager.SteamId64;

                if (!string.IsNullOrEmpty(steamId))
                {
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

            await minimumDelay;
            StopDotsAnimation();

            Application.Current.Dispatcher.Invoke(() =>
            {
                try { ViewNavigator.Instance?.ShowMain(playerInfo, SessionManager.LastLocation); }
                catch { }
            });
        }
    }
}