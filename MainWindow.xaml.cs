using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
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

using TwitchLib;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.PubSub;

namespace ChitchatBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Base = this;
            InitializeComponent();

            var login = new SignIn();
            login.ShowDialog();

            TwitchBot(user, auth, channel);
        }

        public static MainWindow Base;
        private EventLog Log;

        public string
            user, auth, channel;
        public string botPath;
        public static TimedMessage[] TimedMsgs = new TimedMessage[100];
        public static TwitchClient client;
        private void On_Load(object sender, RoutedEventArgs e)
        {
            Log = EventLog.Log;

            string path = "Users";
            botPath = "Bot_" + user + @"\";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            if (!Directory.Exists("Bot_" + user))
                Directory.CreateDirectory("Bot_" + user);
        }

        public void TwitchBot(string user, string auth, string channel)
        {
            client = new TwitchClient();

            ConnectionCredentials info = new ConnectionCredentials(user, auth);

            client.Initialize(info, channel);

            client.OnConnectionError += Connect_Error;
            client.OnConnected += Bot_Connected;
            client.OnJoinedChannel += Bot_Joined;
            client.OnUserJoined += User_Joined;
            client.OnMessageReceived += Message_Received;

            client.Connect();
        }

        #region Twitch Input
        private void Connect_Error(object sender, OnConnectionErrorArgs e)
        {
            LogText(MessageType.Error);

            client.Disconnect();

            var login = new SignIn();
            login.ShowDialog();
        }
        private void Bot_Connected(object sender, OnConnectedArgs e)
        {
            string file = botPath + "Scheduled.txt";
            string[] lines;
            string[][] scheduled = new string[101][];
            if (File.Exists(file))
            {
                using (StreamReader sr = new StreamReader(file))
                    lines = sr.ReadToEnd().Split(';');

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Length > 4)
                    {
                        scheduled[i] = new string[4];
                        scheduled[i][0] = lines[i].Split(':')[0];
                        scheduled[i][1] = lines[i].Split(':')[1];
                        scheduled[i][2] = lines[i].Split(':')[2];
                        scheduled[i][3] = lines[i].Split(':')[3];
                    }
                }
                for (int j = 0; j < scheduled.GetLength(0); j++)
                {
                    if (scheduled[j] != null && scheduled[j].Length > 0)
                    {
                        int frequency;
                        int chance;
                        int.TryParse(scheduled[j][1], out frequency);
                        int.TryParse(scheduled[j][2], out chance);

                        TimedMessage.NewMessage(scheduled[j][0], scheduled[j][3], frequency, chance);
                    }
                }
            }
        }
        private void Bot_Joined(object sender, OnJoinedChannelArgs e)
        {

        }
        private void User_Joined(object sender, OnUserJoinedArgs e)
        {
            string path = @"Users\" + e.Username;
            string file = @"\Data.txt";
            WriteUserData(path, file);
        }
        private void Message_Received(object sender, OnMessageReceivedArgs e)
        {
            string message = e.ChatMessage.Message;
            string cmdsPath = botPath + "Commands.txt";
            string userPath = @"Users\" + user + @"\Data.txt";

            bool command = message.StartsWith("!");
            bool bc = e.ChatMessage.IsBroadcaster;
            bool permit = e.ChatMessage.IsBroadcaster || e.ChatMessage.IsModerator;

            if (command && (bc || UserPermit(userPath)))
            {
                string editCmd = "!edit";
                string delCmd = "!delcmd";
                string taskMsg = "!task";
                string delTask = "!deltask";
                if (message.StartsWith(editCmd))
                {
                    int startIndex = editCmd.Length + 1;
                    string substring = message.Substring(startIndex);
                    int length = Math.Max(startIndex - substring.IndexOf(' '), substring.IndexOf(' ') - startIndex);
                    WriteToFile(cmdsPath, ';', message.Substring(startIndex, length), substring.Substring(length), false);
                }
                if (message.StartsWith(delCmd))
                {
                    int startIndex = delCmd.Length + 1;
                    string substring = message.Substring(startIndex);
                    int length = Math.Max(startIndex - substring.IndexOf(' '), substring.IndexOf(' ') - startIndex);
                    WriteToFile(cmdsPath, ';', message.Substring(startIndex, length), string.Empty, false);
                }
                if (message.StartsWith(taskMsg))
                {
                    string nameIndex = message.Substring(message.IndexOf('$') + 1);
                    string name = nameIndex.Substring(0, nameIndex.IndexOf(' '));
                    string msg = message.Substring(message.IndexOf('$') + name.Length + 2);
                    int frequency;
                    int chance;
                    int.TryParse(message.Substring(message.IndexOf('#') + 1, 2), out frequency);
                    int.TryParse(message.Substring(message.LastIndexOf('#') + 1, 1), out chance);

                    int taskID = TimedMessage.NewMessage(name, msg, frequency, chance);

                    string taskAlert = "@" + e.ChatMessage.Username + " Task ID: " + taskID + " created";
                    client.SendMessage(channel, taskAlert);
                }
                if (message.StartsWith(delTask))
                {
                    string ID = message.Substring(delTask.Length + 1);
                    int id;
                    int.TryParse(ID, out id);
                    foreach (TimedMessage task in TimedMsgs)
                    {
                        if (task != null && (task.name == ID || task.ID == id))
                        {
                            task.Dispose();
                            break;
                        }
                    }
                }
            }
            if (command && FileCheck(cmdsPath, ';', message))
            {
                client.SendMessage(channel, MessageOutput(cmdsPath, ';', message));
            }
        }
        #endregion

        private string
            connectErr = "Connection values either invalid or there is another problem" + "\n",
            setupMsg = "File system setup complete for first time use" + "\n",
            newCmd = "Command has been created" + "\n",
            editCmd = "Command has been modified" + "\n";

        private void LogText(MessageType type)
        {
            if (Log == null)
                Log = new EventLog();
            /*
            switch (type)
            {
                case MessageType.Error:
                    Log.LogOutput.AppendText(connectErr);
                    break;
                case MessageType.Setup:
                    Log.LogOutput.AppendText(setupMsg);
                    break;
                case MessageType.NewCmd:
                    Log.LogOutput.AppendText(newCmd);
                    break;
                case MessageType.Edit:
                    Log.LogOutput.AppendText(editCmd);
                    break;
                default:
                    break;
            }
            */
        }

        public void WriteUserData(string path, string file)
        {
            string date = DateTime.Today.ToShortDateString();

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            if (!File.Exists(path + file))
            {
                using (StreamWriter sw = new StreamWriter(path + file))
                {
                    string[] lines = new string[]
                    {
                        "<date> :" + date,
                        "<first> :" + date,
                        "<permit> :false;",
                        "<count> :0",
                        "<currency> :0",
                        "<warning> :0"
                    };

                    foreach (string s in lines)
                        sw.Write(s + "\n");
                }
            }
            else
            {
                string oldDate = default(string);
                string[] lines;
                using (StreamReader sr = new StreamReader(path + file))
                {
                    lines = sr.ReadToEnd().Split('\r');
                    string s = lines.TakeWhile(line => line.StartsWith("<date>")).ToString();
                    oldDate = s.Substring(s.IndexOf(':') + 1);
                }
                if (date != oldDate)
                {
                    using (StreamReader sr = new StreamReader(path + file))
                        lines = sr.ReadToEnd().Split('\r');
                    using (StreamWriter sw = new StreamWriter(path + file))
                    {
                        for (int i = 0; i < lines.Length; i++)
                        {
                            int count;
                            if (lines[i].Contains("<count>"))
                            {
                                int.TryParse(lines[i].Substring(lines[i].IndexOf(':') + 1), out count);
                                count++;
                                lines[i] = "<count> :" + count;
                            }
                            if (lines[i].Contains("<date>"))
                                lines[i] = "<date> :" + date;
                        }
                        foreach (string s in lines)
                        {
                            if (s.Contains("\n"))
                                sw.Write(s.Substring(s.LastIndexOf('\n')) + 1 + sw.NewLine);
                            else
                                sw.Write(s + sw.NewLine);
                        }
                    }
                }
            }
        }
        public void WriteToFile(string file, char separator, string name, string message, bool delete)
        {
            if (!File.Exists(file))
                using (StreamWriter sw = new StreamWriter(file))
                    sw.Write(";");

            string command = "!" + name + message;
            string[] lines;
            using (StreamReader sr = new StreamReader(file))
                lines = sr.ReadToEnd().Split(separator);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (delete)
                {
                    if (line.Contains("!" + name))
                        lines[i] = string.Empty;
                }
                else
                {
                    if (line.Contains("!" + name))
                    {
                        lines[i] = command;
                        LogText(MessageType.Edit);
                        break;
                    }
                    if (line.Length < 5)
                    {
                        lines[i] = command;
                        LogText(MessageType.NewCmd);
                        break;
                    }
                }
            }
            using (StreamWriter sw = new StreamWriter(file))
            {
                foreach (string s in lines)
                {
                    if (s.Length > 4)
                    {
                        if (s.Contains("\n"))
                            sw.Write(s.Substring(s.LastIndexOf("\n") + 1) + separator + sw.NewLine);
                        else
                            sw.Write(s + separator + sw.NewLine);
                    }
                }
            }
        }
        public bool UserPermit(string file)
        {
            string[] lines;
            bool flag = false;
            using (StreamReader sr = new StreamReader(file))
                lines = sr.ReadToEnd().Split('\r');
            foreach (string s in lines)
            {
                if (s.Contains("<permit>"))
                {
                    bool.TryParse(s.Substring(s.IndexOf(":") + 1), out flag);
                    break;
                }
            }
            return flag;
        }
        public bool FileCheck(string file, char separator, string name)
        {
            string[] lines;
            using (StreamReader sr = new StreamReader(file))
                lines = sr.ReadToEnd().Split(separator);
            foreach (string s in lines)
            {
                if (s.Contains(name))
                    return true;
            }
            return false;
        }
        public string MessageOutput(string file, char separator, string name)
        {
            string[] lines;
            using (StreamReader sr = new StreamReader(file))
                lines = sr.ReadToEnd().Split(separator);
            foreach (string s in lines)
            {
                if (s.Contains(name))
                    return s.Substring(name.Length + 1);
            }
            return string.Empty;
        }
    }
    enum MessageType
    {
        Error,
        Setup,
        Info,
        NewCmd,
        Edit
    }

    public class TimedMessage : IDisposable
    {
        public bool active;
        public string name;
        public string message;
        public int frequency;
        public int chance;
        public int ID;
        private Timer schedule;
        private static MainWindow Base;
        private Random rand
        {
            get { return new Random(); }
        }
        public TimedMessage()
        {
            Base = MainWindow.Base;
        }
        public static int NewMessage(string name, string message, int frequency, int chance)
        {
            int num = 101;
            for (int i = 0; i < MainWindow.TimedMsgs.Length; i++)
            {
                if (MainWindow.TimedMsgs[i] == null || !MainWindow.TimedMsgs[i].active)
                {
                    num = i;
                    break;
                }
            }
            MainWindow.TimedMsgs[num] = new TimedMessage();
            foreach (TimedMessage task in MainWindow.TimedMsgs)
            {
                if (task != null && task.name == name)
                {
                    num = task.ID;
                    task.Dispose();
                    break;
                }
            }
            MainWindow.TimedMsgs[num].name = name;
            MainWindow.TimedMsgs[num].message = message;
            MainWindow.TimedMsgs[num].frequency = frequency;
            MainWindow.TimedMsgs[num].chance = chance;
            MainWindow.TimedMsgs[num].active = true;
            MainWindow.TimedMsgs[num].ID = num;
            MainWindow.TimedMsgs[num].WriteToFile(';', ':', false);
            MainWindow.TimedMsgs[num].Setup();
            return num;
        }
        public void Dispose()
        {
            WriteToFile(';', ':', true);

            active = false;
            schedule.Stop();
            schedule.Dispose();
            MainWindow.TimedMsgs[ID] = null;
        }

        private void Setup()
        {
            schedule = new Timer();
            schedule.Interval = frequency * 600;
            schedule.BeginInit();
            schedule.Start();
            schedule.Elapsed += SendMessage;
        }
        private void SendMessage(object sender, ElapsedEventArgs e)
        {
            if (rand.Next(chance) == 0)
                MainWindow.client.SendMessage(Base.channel, message);
        }

        private void WriteToFile(char separator, char split, bool delete)
        {
            string file = Base.botPath + "Scheduled.txt";
            if (!File.Exists(file))
                using (StreamWriter sw = new StreamWriter(file))
                    sw.Write(' ');

            string[] lines;
            using (StreamReader sr = new StreamReader(file))
                lines = sr.ReadToEnd().Split(separator);

            for (int i = 0; i < lines.Length; i++)
            {
                if (delete)
                {
                    if (lines[i].Length > 4 && lines[i].Substring(0, name.Length + 1) == name + split)
                    {
                        lines[i] = string.Empty;
                        break;
                    }
                }
                else if (lines[i].Length > 4)
                {
                    if (lines[i].Substring(0, name.Length) == name)
                    {
                        lines[i] = name + split + frequency + split + chance + split + message;
                        break;
                    }
                    if (lines[i].Substring(lines[i].LastIndexOf('\n') + 1).StartsWith(";"))
                    {
                        lines[i] = name + split + frequency + split + chance + split + message;
                        break;
                    }
                }
                else
                {
                    lines[i] = name + split + frequency + split + chance + split + message;
                    break;
                }
            }
            using (StreamWriter sw = new StreamWriter(file))
            {
                foreach (string s in lines)
                {
                    if (s.Contains("\n") && s.Contains(split))
                        sw.Write(s.Substring(s.LastIndexOf("\n")) + separator + sw.NewLine);
                    else if (s.Contains(split))
                        sw.Write(s + separator + sw.NewLine);
                }
            }
        }
    }
}
