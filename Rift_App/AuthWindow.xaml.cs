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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Rift_App
{
    public partial class AuthWindow : Window
    {
        public AuthWindow()
        {
            InitializeComponent();

            // Closing = hide, not exit — zatvorenie = skryje sa, neukončí app
            this.Closing += (s, e) =>
            {
                e.Cancel = true;
                this.Hide();
            };
        }
        public void ShowView(UserControl view)
        {
            try
            {
                Content = view;
            }
            catch { }
        }
    }
}