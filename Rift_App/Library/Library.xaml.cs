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

namespace Rift_App.Library
{
    /// <summary>
    /// Interaction logic for Library.xaml
    /// </summary>
    public partial class Library : UserControl
    {
        public Library()
        {
            InitializeComponent();

            for(int i = 1; i <= 60; i++)
{
                var button = new Button
                {
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Height = 23
                };

                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Width = 235,
                    Height = 22
                };

                var image = new Image
                {
                    Height = 20,
                    Width = 20,
                    Margin = new Thickness(5, 0, 0, 0),
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Source = new BitmapImage(new Uri("/Icons/Red.png", UriKind.Relative))
                };

                var textBlock = new TextBlock
                {
                    Text = "Game " + i,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9197A5")),
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(6,-2, 0, 0)
                };

                stackPanel.Children.Add(image);
                stackPanel.Children.Add(textBlock);
                button.Content = stackPanel;
                MyStackPanel.Children.Add(button);
            }

        }
    }
}
