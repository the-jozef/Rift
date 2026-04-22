using Rift_App.AppController;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Rift_App.Login_Register
{
    public partial class Login_loading : UserControl
    {
        private VideoBackground_Loading _videoController;

        // Event, ak by si ho niekedy potreboval (pre flags v ViewModel)
        public event EventHandler? VideoReady;

        public Login_loading()
        {
            InitializeComponent();

            // Nastavíme controller hneď pri vytvorení
            _videoController = new VideoBackground_Loading(BgVideo);
            _videoController.VideoReady += OnVideoReady;

            // Loaded používame len na spustenie videa OD ZAČIATKU pri zobrazení
            this.Loaded += OnLoaded;
        }

        /// <summary>
        /// Spustí načítanie videa čo najskôr (volané z ViewModelu v konštruktore)
        /// </summary>
        public void PreloadVideo()
        {
            _videoController.Start();
        }

        private void OnVideoReady(object sender, EventArgs e)
        {
            VideoReady?.Invoke(this, e);

            // Po načítaní videa ho hneď zastavíme (nechceme, aby bežalo na pozadí pred zobrazením)
            if (BgVideo != null)
            {
                BgVideo.Pause();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Keď sa UserControl zobrazí → spustíme video OD ZAČIATKU
            if (BgVideo != null)
            {
                BgVideo.Position = TimeSpan.Zero;
                BgVideo.Play();
            }
        }
    }
}