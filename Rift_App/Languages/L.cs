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
                var value = Application.Current.Resources[key] as string ?? key;
                return value;
            }
            catch
            {
                return key;
            }
        }
    }
}
