using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Rift_App.ViewModels
{
    /// <summary>
    /// Wrapper pre obrázok hry — async načíta a uloží do ImageCacheService.
    /// Wrapper for a game image — async loads and stores in ImageCacheService.
    /// 
    /// Použitie v XAML — Usage in XAML:
    /// Source="{Binding GameImage.Bitmap}"
    /// 
    /// GameImage = new GameImageViewModel(url) — spustí async load
    /// </summary>
    public class GameImageViewModel : INotifyPropertyChanged
    {
        private BitmapImage? _bitmap;

        // Obrázok keď je načítaný — image when loaded
        public BitmapImage? Bitmap
        {
            get => _bitmap;
            private set { _bitmap = value; OnPropertyChanged(); }
        }

        public bool IsLoaded => _bitmap != null;

        public string Url { get; }

        public GameImageViewModel(string url)
        {
            Url = url;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (string.IsNullOrEmpty(Url)) return;
            Debug.WriteLine($"[ImageVM] Loading: {Url}");
            Bitmap = await ImageCacheService.GetAsync(Url);
            Debug.WriteLine($"[ImageVM] Result: {(Bitmap != null ? "OK" : "NULL")} for {Url}");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}