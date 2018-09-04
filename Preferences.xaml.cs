using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;

namespace ChitchatBot
{
    /// <summary>
    /// Interaction logic for Preferences.xaml
    /// </summary>
    public partial class Preferences : Window
    {
        public Preferences()
        {
            InitializeComponent();
        }

        private MainWindow Base;
        private void On_Load(object sender, RoutedEventArgs e)
        {
            Base = MainWindow.Base;
            string path = Base.botPath + "Preferences.txt";
            string[] lines;

            FileStart(path);
            using (StreamReader sw = new StreamReader(path))
                lines = sw.ReadToEnd().Split(';');

            Box1_CmdsModify.Text = lines[0];
            Box2_CmdsDelete.Text = lines[1];
            Box3_TasksModify.Text = lines[2];
            Box4_TasksDelete.Text = lines[3];
            Box5_Help.Text = lines[4];
            Box6_UserWord.Text = lines[5];
            Box7_UserPermit.Text = lines[6];
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string path = Base.botPath + "Preferences.txt";
            string[] lines = new string[7];

            FileStart(path);

            lines[0] = Box1_CmdsModify.Text;
            lines[1] = Box2_CmdsDelete.Text;
            lines[2] = Box3_TasksModify.Text;
            lines[3] = Box4_TasksDelete.Text;
            lines[4] = Box5_Help.Text;
            lines[5] = Box6_UserWord.Text;
            lines[6] = Box7_UserPermit.Text;

            using (StreamWriter sw = new StreamWriter(path))
            {
                foreach (string s in lines)
                    sw.Write(s + ";");
            }
            Base.userPref = lines;

            Close();
        }

        private void FileStart(string path)
        {
            if (!File.Exists(path))
            {
                using (StreamWriter sw = new StreamWriter(path))
                {
                    foreach (string s in Base.userPref)
                        sw.Write(s + ";");
                }
            }
        }
    }
}
