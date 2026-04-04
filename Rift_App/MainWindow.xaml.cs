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
            DataContext = new WindowStateViewModel();
    
            Authencation authencation = new Authencation();
            authencation.Show();
            this.Close();


            /*
          Window_Test test = new Window_Test();
                    test.Show();
                    this.Close();
                    /*
                    Authencation authencation = new Authencation();
                    authencation.Show();
                    this.Close();








                    /*
                     // === TESTOVANIE REGISTRÁCIE ===(urobu noveho hraca automaticky pri spusteni appky)

                    var auth = new AuthService();

                    // === OPRÁVENE VOLANIE REGISTER ===
                    string errorMessage;
                    bool success = auth.Register("testuser", "mojeheslo123", "76561197960287930", out errorMessage);

                    if (success)
                    {
                        MessageBox.Show("Testový používateľ bol úspešne zaregistrovaný!",
                                        "Registrácia OK",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Registrácia zlyhala:\n{errorMessage}",
                                        "Chyba registrácie",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                    }
                    */




        }
    }
}