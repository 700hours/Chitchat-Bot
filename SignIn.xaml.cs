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
            Log = EventLog.Log;
            Base = MainWindow.Base;

            if (!File.Exists(path))
            {
                using (StreamWriter sw = new StreamWriter(path))
                    sw.Write("bot_name channel_name bot_oauth");
            }
            else
            {
                using (StreamReader sr = new StreamReader(@path))
                {
                    string[] lines = sr.ReadToEnd().Split(' ');
                    if (lines.Length > 1)
                    {
                        Username.Text = lines[0];
                        Channel.Text = lines[1];
                        Auth.Text = lines[2];
                    }
                }
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (Log == null || !Log.IsActive)
            {
                Log = new EventLog();
                Log.Show();
            }
            bool valid = Username.Text.Length > 3 && Channel.Text.Length > 3 && Auth.Text.Length > 20 && Auth.Text.ToLower().Contains("oauth");

            if (Username.Text.Length < 4)
                Log.LogOutput.AppendText(usernameErr + "\n");
            if (Channel.Text.Length < 4)
                Log.LogOutput.AppendText(channelErr + "\n");
            if (Auth.Text.Length < 20)
                Log.LogOutput.AppendText(authErr + "\n");
            if (!Auth.Text.ToLower().Contains("oauth"))
                Log.LogOutput.AppendText(oauthErr + "\n");

            if (valid)
            {
                Log.LogOutput.AppendText("Attempting to connect" + "\n");

                Base.user = Username.Text;
                Base.auth = Auth.Text;
                Base.channel = Channel.Text;

                Close();
            }
        }

    }
}
