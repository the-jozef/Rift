using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Rift_App.AppController
{
    public class VideoBackground_Loading
    {
        private readonly MediaElement _videoPlayer;
        public event EventHandler VideoReady;

    
        public VideoBackground_Loading(MediaElement videoPlayer)
        {
            _videoPlayer = videoPlayer;

            _videoPlayer.MediaOpened += OnMediaOpened;
            _videoPlayer.MediaEnded += OnMediaEnded;
            _videoPlayer.Play();
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            VideoReady?.Invoke(this, EventArgs.Empty);
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _videoPlayer.Position = TimeSpan.Zero;
            _videoPlayer.Play();
        }


    }
}
