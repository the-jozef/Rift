using System;
using System.Collections.Generic;
using System.Text;
using Rift_App.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Rift_App.ViewModels
{
    public class SearchResultImageViewModel : INotifyPropertyChanged
    {
        private static readonly string[] FallbackPaths =
        {
            "header.jpg",           // 460×215
            "capsule_616x353.jpg",  // 616×353
            "capsule_sm_120.jpg",   // 120×45
            "logo.png",             // logo
        };

        private BitmapImage? _bitmap;
        public BitmapImage? Bitmap
        {
            get => _bitmap;
            private set { _bitmap = value; OnPropertyChanged(); }
        }

        public SearchResultImageViewModel(int appId)
        {
            _ = LoadWithFallbackAsync(appId);
        }

        private async Task LoadWithFallbackAsync(int appId)
        {
            foreach (var path in FallbackPaths)
            {
                var url = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/{path}";
                var bitmap = await ImageCacheService.GetAsync(url);
                if (bitmap != null)
                {
                    Bitmap = bitmap;
                    return;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}