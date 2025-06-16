using HazyBits.Twain.Cloud.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TwainDirect.Scanner
{
    internal class Utils
    {
        public static FormLogin GetLoginForm(string url)
        {
            Boolean isWindowsPlatform = Environment.OSVersion.ToString().Contains("Microsoft Windows");
            FormLogin loginForm;
  
            if (isWindowsPlatform)
            {
                loginForm = new FormWebview(url);
            }
            else
            {
                loginForm = new FacebookLoginForm(url);
            }

            return loginForm;
        }
    }
}
