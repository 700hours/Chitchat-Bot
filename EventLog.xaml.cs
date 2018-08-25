using System;
using System.Collections.Generic;
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
    /// Interaction logic for EventLog.xaml
    /// </summary>
    public partial class EventLog : Window
    {
        public static EventLog Log;
        public EventLog()
        {
            InitializeComponent();
            Show();
        }

        private void On_Load(object sender, RoutedEventArgs e)
        {
            Log = this;
            LogOutput.IsReadOnly = true;
        }
        private void Text_Update(object sender, TextChangedEventArgs e)
        {
            LogOutput.ScrollToEnd();
        }
    }
}
