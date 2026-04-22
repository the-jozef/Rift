using Rift_App.AppController;
using Rift_App.ViewModels;
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
    public partial class Login_loading : UserControl
    {
        private MediaClock _mediaClock;

        public Login_loading()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("Videos/Background_animation.mp4", UriKind.Relative);
            var timeline = new MediaTimeline(uri) { RepeatBehavior = RepeatBehavior.Forever };
            _mediaClock = timeline.CreateClock();
            BgVideo.Clock = _mediaClock;
            _mediaClock.Controller.Begin();
        }
    }
    //_videoController = new VideoBackground_Loading(BgVideo);
    //_videoController.VideoReady += (s, e) => VideoReady?.Invoke(this, e);
    //this.Loaded += (s, e) => _videoController.Start();
}
