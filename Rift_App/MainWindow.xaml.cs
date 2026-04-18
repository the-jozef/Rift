using CommunityToolkit.Mvvm.Input;
using Rift_App.Account;
using Rift_App.Database;
using Rift_App.Login_Register;
using Rift_App.Testing;
using Rift_App.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Rift_App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //DataContext = new MainViewModel();
            DataContext = new WindowViewModel();





            // var testacc = new TestWindowAcc();  //Account testing window
            //testacc.Show();
        }
      
        /*
               

         Window_test2 test = new Window_test2();
            test.Show();
            this.Close();
           
        */
    }
}