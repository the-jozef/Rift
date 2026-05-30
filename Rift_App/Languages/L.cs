using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Rift_App.Languages
{
    public static class L
    {
        public static string Get(string key)
        {
            try
            {
                return Application.Current.Resources[key] as string ?? key;
            }
            catch
            {
                return key;
            }
        }
    }
}
