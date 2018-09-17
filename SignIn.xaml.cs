using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ChitchatBot
{
    /// <summary>
    /// Interaction logic for SignIn.xaml
    /// </summary>
    public partial class SignIn : Window
    {
        public SignIn()
        {
            InitializeComponent();
        }

        private string
            usernameErr = "User name is invalid",
            channelErr = "Channel name is invalid",
            oauthErr = "Include the OAuth: text before the auth key",
            authErr = "Auth key is invalid",
            path = "Login_Values.txt";
        private EventLog Log;
        private MainWindow Base;
        private void On_Load(object sender, RoutedEventArgs e)
        {
            Base = MainWindow.Base;
            string[] lines;
            if (!File.Exists(path))
            {
                using (StreamWriter sw = new StreamWriter(path))
                    sw.Write("bot_name channel_name bot_oauth access_token");
            }
            else
            {
                using (StreamReader sr = new StreamReader(@path))
                {
                    lines = sr.ReadToEnd().Split(' ');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i == 0)
                            Username.Text = lines[i];
                        if (i == 1)
                            Channel.Text = lines[i];
                        if (i == 2)
                            Auth.Text = lines[i];
                        if (i == 3)
                            Token.Text = lines[i];
                    }
                }
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            bool valid = Username.Text.Length > 3 && Channel.Text.Length > 3 && Auth.Text.Length > 20 && Auth.Text.ToLower().Contains("oauth") && Token.Text.Length == 30;

            if (valid)
            {
                Base.user = Username.Text;
                Base.auth = Auth.Text;
                Base.channel = Channel.Text;
                Base.token = Token.Text;

                using (StreamWriter sw = new StreamWriter(path))
                    sw.Write(Base.user + " "
                        + Base.channel + " "
                        + Base.auth + " " 
                        + Base.token);

                Close();
            }
        }
    }
}
