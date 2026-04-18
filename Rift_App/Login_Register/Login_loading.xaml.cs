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
        private VideoBackground_Loading _videoController;

        public event EventHandler VideoReady;
        public Login_loading()
        {
            InitializeComponent();
            _videoController = new VideoBackground_Loading(BgVideo);
            _videoController.VideoReady += (s, e) => VideoReady?.Invoke(this, e);

        }
  
    }
}
