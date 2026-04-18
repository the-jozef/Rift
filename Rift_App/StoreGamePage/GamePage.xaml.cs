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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App.StoreGamePage
{
    /// <summary>
    /// Interaction logic for GamePage.xaml
    /// </summary>
    public partial class GamePage : UserControl
    {
        public GamePage()
        {
            InitializeComponent();
        }

        private void MediaScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MediaScroll.ScrollToHorizontalOffset(
                MediaScroll.HorizontalOffset - e.Delta / 3.0);
            e.Handled = true;
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {

        }
    }
}
