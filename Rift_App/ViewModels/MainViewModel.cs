using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Rift_App.ViewModels
{
    public class MainViewModel : ViewBase
    {
        public Navigator Navigator { get; }
        public WindowStateViewModel WindowState { get; }
        public WindowViewModel WindowViewModel { get; }

        public MainViewModel()
        {
            Navigator = new Navigator();
            WindowState = new WindowStateViewModel();
            WindowViewModel = new WindowViewModel();
        }
    }
}
