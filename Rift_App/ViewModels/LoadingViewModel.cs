using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Rift_App.Models;
using Rift_App.Services;
using System.Windows;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using Rift_App.Languages;

namespace Rift_App.ViewModels
{
    public partial class LoadingViewModel : ObservableObject
    {
        [ObservableProperty] private string _loadingText = L.Get("loading_text");

        private CancellationTokenSource? _dotsCts;
        public LoadingViewModel()
        {
            LanguageService.LanguageChanged += RefreshTexts;
        }

        private void RefreshTexts()
        {
            //Restart animation with new language
            StopDotsAnimation();
            StartDotsAnimation();
        }

        private void StartDotsAnimation()
        {
            _dotsCts?.Cancel();
            _dotsCts = new CancellationTokenSource();
            var token = _dotsCts.Token;

            Task.Run(async () =>
            {
                int i = 0;
                while (!token.IsCancellationRequested)
                {
                    string base_ = L.Get("loading_text");
                    string[] frames =
                    {
                    base_,
                    base_ + " .",
                    base_ + " ..",
                    base_ + " ..."
                };
                    var frame = frames[i % frames.Length];
                    Application.Current.Dispatcher.Invoke(() => LoadingText = frame);
                    i++;
                    await Task.Delay(800, token).ContinueWith(_ => { });
                }
            });
        }
    
        private void StopDotsAnimation() => _dotsCts?.Cancel();

        // ─── MODE 1: App startup ──────────────────────────────────────────
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

        // ─── MODE 2: After login / account switch ─────────────────────────
        public async Task LoadSteamDataAsync()
        {
            StartDotsAnimation();
            PlayerInfo? playerInfo = null;

            var minimumDelay = Task.Delay(TimeSpan.FromSeconds(3));

            try
            {
                var steamId = SessionManager.SteamId64;

                if (!string.IsNullOrEmpty(steamId))
                {
                    // ── Player info ───────────────────────────────────────
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

                    // ── Preload store featured section in background ───────
                    // This warms the backend cache so the Store page opens fast.
                    // Fire-and-forget: we don't wait for it and it won't block login.
                    _ = PreloadStoreFeaturedAsync();

                    // ── Preload wishlist count independently ──────────────
                    _ = WishlistCountCache.GetAsync();

                    // ── Ensure LastPlayed cache is ready ──────────────────
                    _ = LastPlayedCacheService.InitializeAsync();
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

        // ─── STORE PRE-WARM ───────────────────────────────────────────────
        private static async Task PreloadStoreFeaturedAsync()
        {
            try
            {
                // Only preload if the section list cache is stale
                var cached = await StoreGameCacheService.LoadSectionAsync(
                    StoreGameCacheService.KeyFeatured);

                if (cached != null && cached.Count >= 6)
                {
                    Debug.WriteLine("[Loading] Store featured cache is warm — skipping preload.");
                    return;
                }

                Debug.WriteLine("[Loading] Pre-warming store featured section...");
                var games = await ApiService.GetFeaturedAsync();
                if (games.Count > 0)
                    await StoreGameCacheService.SaveSectionAsync(
                        StoreGameCacheService.KeyFeatured, games);

                Debug.WriteLine($"[Loading] Pre-warm done: {games.Count} featured games cached.");
            }
            catch (Exception ex)
            {
                // Non-critical — store will load normally if this fails
                Debug.WriteLine($"[Loading] PreloadStore error: {ex.Message}");
            }
        }
    }
}