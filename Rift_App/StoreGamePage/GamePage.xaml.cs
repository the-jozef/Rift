using Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public partial class GamePage : UserControl
    {
        private GameModel? _currentGame;

        public GamePage() => InitializeComponent();


        public void LoadGame(GameModel game)
        {
            _currentGame = game;
            DataContext = game;
        }
    }
}