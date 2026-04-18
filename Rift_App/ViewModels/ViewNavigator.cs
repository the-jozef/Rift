using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Rift_App.Login_Register;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rift_App.ViewModels
{
    public partial class Navigator : ViewBase
    {
        public ICommand ShowAuthencationCommand {  get; }


        public Navigator()
        {
            ShowAuthencationCommand = new RelayCommand(
                () =>
                {
                    var auth = new Authencation();
                    auth.Show();

                });



        }



    }
}
