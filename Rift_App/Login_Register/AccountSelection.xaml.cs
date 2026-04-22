using Rift_App.AppController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App.Login_Register
{
    public partial class AccountSelection : UserControl
    {
        private VideoBackground_Loading _videoController;

        public event EventHandler VideoReady;
        public AccountSelection()
        {
            InitializeComponent();

            _videoController = new VideoBackground_Loading(BgVideo);
            _videoController.VideoReady += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                    fadeOut.Completed += (_, __) => VideoOverlay.Visibility = Visibility.Collapsed;
                    VideoOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    VideoReady?.Invoke(this, e);
                });
            };
            this.Loaded += (s, e) => _videoController.Start();
        }
    }
}
