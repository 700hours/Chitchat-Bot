using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Speech.Synthesis;
using System.Text;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using TwitchLib;
using TwitchLib.Api;
using TwitchLib.Api.Models;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

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
        public string
            user, auth, channel, token;
        public string
            channelID, channelAuth;
        private string clientID = "eh5c6tkcr40gkvhg84eqda94hh11ns";
        public string botPath;
        private string selected;
        private string hlPath;
        public string[] cmdsList;
        public string[] userPref = new string[]
        {
            "!edit",
            "!delcmd",
            "!task",
            "!deltask",
            "!help",
            "!choose",
            "!permit"
        };
        private bool CanInteract = true;
        private bool CanWhisper = true;
        private bool ViewChat;
        public static TimedMessage[] TimedMsgs = new TimedMessage[100];
        public static ActiveUsers[] activeUsers = new ActiveUsers[1001];
        private System.Timers.Timer Elapsed;
        public static TwitchClient client;
        public static TwitchAPI api;
        private void On_Load(object sender, RoutedEventArgs e)
        {
            string path = "Users";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            if (!Directory.Exists("Bot_" + user))
                Directory.CreateDirectory("Bot_" + user);

            path = botPath + "Preferences.txt";
            FileStart(path, WriteType.Pref);
            hlPath = botPath + "Highlights" + "_" + DateTime.Today.Month + "-" + DateTime.Today.Day + "-" + DateTime.Today.Year + ".txt";
            FileStart(hlPath, WriteType.Data);
            string[] lines;

            using (StreamReader sw = new StreamReader(path))
                lines = sw.ReadToEnd().Split(';');
            userPref = lines;

            UpdateList();
        }
        private void On_Closed(object sender, EventArgs e)
        {
            if (storyThread != null)
                storyThread.Abort();
        }

        public void TwitchBot(string user, string auth, string channel)
        {
            client = new TwitchClient();

            ConnectionCredentials info = new ConnectionCredentials(user, auth);

            client.Initialize(info, channel);

            client.AddChatCommandIdentifier('!');

            client.OnConnectionError += Connect_Error;
            client.OnConnected += Bot_Connected;
            client.OnJoinedChannel += Bot_Joined;
            client.OnUserJoined += User_Joined;
            client.OnMessageReceived += Message_Received;
            client.OnChatCommandReceived += Command_Recieved;
            client.OnNewSubscriber += New_Sub;
            client.OnGiftedSubscription += Gift_Sub;
            client.OnReSubscriber += Resub_Recieved;
            client.OnChannelStateChanged += Channel_State;

            client.Connect();

            api = new TwitchAPI();
            api.Settings.ClientId = clientID;
            api.Settings.AccessToken = token;
            InfoCheck();

            Elapsed = new System.Timers.Timer(3000);
            Elapsed.Elapsed += CanSend;
            Elapsed.Start();
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
            botPath = "Bot_" + user + @"\";
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
                int count = 0;
                foreach (TimedMessage msg in TimedMsgs)
                {
                    if (msg != null)
                        count++;
                }
                string text = count + " scheduled message[s] started";
                client.SendMessage(channel, text);
            }

            file = botPath + "Blacklist.txt";
            if (!File.Exists(file))
            {
                using (StreamWriter sw = new StreamWriter(file))
                    sw.Write("Replace this with words or phrases separated by commas with no spaces inbetween");
            }
        }
        private void Bot_Joined(object sender, OnJoinedChannelArgs e)
        {
            return;
        }
        private void User_Joined(object sender, OnUserJoinedArgs e)
        {
            string path = @"Users\" + e.Username;
            string file = @"\Data.txt";
            WriteJoinData(path, file);
        }
        private void Message_Received(object sender, OnMessageReceivedArgs e)
        {
            ActiveUsers.NewUser(e.ChatMessage.Username, 60);

            if (ViewChat)
            {
                ViewChat = false;
            }
        }
        private void Command_Recieved(object sender, OnChatCommandReceivedArgs e)
        {
            string chatUser = e.Command.ChatMessage.Username;
            string message = e.Command.ChatMessage.Message;
            string cmdsPath = botPath + "Commands.txt";
            string userPath = @"Users\" + chatUser + @"\Data.txt";

            bool command = message.StartsWith("!");
            bool bc = e.Command.ChatMessage.IsBroadcaster;
            bool mod = e.Command.ChatMessage.IsModerator;

            string editCmd = userPref[0];   
            string delCmd = userPref[1];    
            string taskMsg = userPref[2];   
            string delTask = userPref[3];   
            string help = userPref[4];      
            string choose = userPref[5];    
            string permit = userPref[6];
            string uptime = "!uptime";
            string highlight = "!highlight";

            cmdsList = new string[]
            {
                editCmd,
                delCmd,
                taskMsg,
                delTask,
                help,
                choose,
                permit
            };

            if (bc || mod || UserPermit(userPath))
            {
                if (message.StartsWith(help) && CanWhisper)
                {
                    string cmdText = "[Command syntax] " + editCmd + "<name> <message> to add, " + editCmd + " <name> to modify, " + delCmd + " to remove";
                    string taskText = "[Task syntax] " + taskMsg + " #<frequency in minutes> #<send chance per frequency> $<name> <message>, " + delTask + " <ID>/<name>";
                    string miscText = "[Extra] " + choose + " <user> when interaction is active";
                    client.SendWhisper(chatUser, cmdText);
                    client.SendWhisper(chatUser, taskText);
                    client.SendWhisper(chatUser, miscText);

                    CanWhisper = false;
                    return;
                }
                if (message.StartsWith(editCmd))
                {
                    int startIndex = editCmd.Length + 1;
                    string substring = message.Substring(startIndex);
                    string name = substring.Substring(0, substring.IndexOf(' '));
                    WriteToFile(cmdsPath, ';', name, substring.Substring(name.Length + 1), false);

                    string text;
                    if (FileCheck(cmdsPath, ';', name))
                        text = "!" + name + " command modified";
                    else
                        text = "!" + name + "command created";
                    client.SendMessage(channel, text);
                    return;
                }
                if (message.StartsWith(delCmd))
                {
                    int startIndex = delCmd.Length + 1;
                    string name = message.Substring(startIndex);
                    WriteToFile(cmdsPath, ';', name, string.Empty, true);

                    string text = "Command !" + name + " removed";
                    client.SendMessage(channel, text);
                    return;
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

                    string taskAlert = "@" + chatUser + " Task ID: " + taskID + " created";
                    client.SendMessage(channel, taskAlert);
                    return;
                }
                if (message.StartsWith(delTask))
                {
                    string name = message.Substring(delTask.Length + 1);
                    int id;
                    int.TryParse(name, out id);
                    foreach (TimedMessage task in TimedMsgs)
                    {
                        if (task != null && (task.name == name || task.ID == id))
                        {
                            task.Dispose();

                            string text = "Task ID: " + id + " removed";
                            client.SendMessage(channel, text);
                            break;
                        }
                    }
                    return;
                }
                if (message.StartsWith(choose) && begun)
                {
                    if (message.Substring(choose.Length + 1) != "random")
                    {
                        selected = message.Substring(choose.Length + 1);
                        string text = "@" + selected + " please provide a " + verbType;
                        client.SendMessage(channel, text);
                    }
                    else
                        SelectAtRandom();
                    return;
                }
                if (bc && message.StartsWith(permit))
                {
                    string specified = message.Substring(permit.Length + 1);
                    string path = @"Users\" + specified;
                    string file = @"\Data.txt";

                    if (File.Exists(path + file))
                        WriteUserData(path, file, specified, DataType.Permit);
                    else
                    {
                        string text = "User data not available";
                        client.SendMessage(channel, text);
                    }
                    return;
                }
                if (message.StartsWith(highlight))
                {
                    var time = api.Streams.v5.GetUptimeAsync(channelID).Result;
                    
                    if (time.HasValue)
                    {
                        int hours = time.Value.Hours;
                        int minutes = time.Value.Minutes;
                        int seconds = time.Value.Seconds;

                        string name = message.Substring(highlight.Length + 1);
                        string timestamp = string.Format("{0}:{1}:{2}", new object[] { hours, minutes, seconds });

                        using (StreamWriter sw = new StreamWriter(hlPath, true))
                            sw.WriteLine(name + " " + timestamp);

                        string text = chatUser + " created highlight named: " + name;
                        client.SendMessage(channel, text);
                    }
                    return;
                }
            }
            if (CanInteract)
            {
                if (message == uptime)
                {
                    GetUptime(chatUser);
                    return;
                }
                if (FileCheck(cmdsPath, ';', message))
                { 
                    client.SendMessage(channel, MessageOutput(cmdsPath, ';', message, chatUser));
                    CanInteract = false;
                    return;
                }
            }
            if (begun && selected == chatUser)
            {
                foreach (string s in cmdsList)
                {
                    if (message.Contains(s))
                        return;
                    else
                    {
                        string file = botPath + "Blacklist.txt";
                        using (StreamReader sr = new StreamReader(file))
                        {
                            string[] list = sr.ReadToEnd().Split(',');
                            foreach (string verb in list)
                            {
                                if (message.Contains(verb))
                                {
                                    string path = @"Users\" + chatUser;
                                    string file2 = @"\Data.txt";
                                    if (File.Exists(path + file2))
                                        WriteUserData(path, file2, chatUser, DataType.Warning);

                                    string notice = "@" + chatUser + " Invalid word was chosen, try with something else";
                                    client.SendMessage(channel, notice);
                                    return;
                                }
                            }
                        }
                        break;
                    }
                }
                selected = string.Empty;
                verbInput = message.Substring(1);
                input = true;

                string text = verbInput + " accepted as a " + verbType.ToLower();
                client.SendMessage(channel, text);
                return;
            }
        }
        private void New_Sub(object sender, OnNewSubscriberArgs e)
        {
            string specified = e.Subscriber.DisplayName;
            string path = @"Users\" + specified;
            string file = @"\Data.txt";

            if (!File.Exists(path + file))
                WriteJoinData(path, file);

            WriteUserData(path, file, specified, DataType.NewSub);
        }
        private void Gift_Sub(object sender, OnGiftedSubscriptionArgs e)
        {
            
        }
        private void Resub_Recieved(object sender, OnReSubscriberArgs e)
        {
            var speech = new SpeechSynthesizer();
            speech.Speak(e.ReSubscriber.ResubMessage);
            speech.Dispose();
            speech = null;
        }
        private void Channel_State(object sender, OnChannelStateChangedArgs e)
        {
            
        }
        #endregion
        private async Task InfoCheck()
        {
            CredentialCheckResponseModel u = await api.Settings.CheckCredentialsAsync();
            TwitchLib.Api.Models.v5.Root.Root check = await api.Root.v5.GetRootAsync(auth.Substring(6), clientID);
            TwitchLib.Api.Models.v5.Root.Root id = await api.Root.v5.GetRootAsync(token, clientID);

            channelID = id.Token.UserId;

            using (StreamWriter sw = new StreamWriter(botPath + @"\Debug.txt"))
            {
                sw.WriteLine(u.ResultMessage);
                sw.WriteLine(" ");
                sw.WriteLine("Valid? " + check.Token.Valid);
                sw.WriteLine(check.Token.Username);
                sw.WriteLine(check.Token.UserId);
                sw.WriteLine(" ");
                sw.WriteLine("Valid? " + id.Token.Valid);
                sw.WriteLine(id.Token.Username);
                sw.WriteLine(channelID);
            }
        }
        private async Task GetUptime(string chatUser)
        {
            bool check = await api.Streams.v5.BroadcasterOnlineAsync(channelID);

            var time = api.Streams.v5.GetUptimeAsync(channelID).Result;
            
            if (time.HasValue)
            {
                int hours = time.Value.Hours;
                int minutes = time.Value.Minutes;
                int seconds = time.Value.Seconds;

                string online = "Stream has been online for ";
                string text = string.Empty;
                if (hours > 0)
                    text += hours + " hours, ";
                if (minutes > 0)
                    text += minutes + " minutes and ";

                text += seconds + " seconds";

                client.SendMessage(channel, chatUser + " " + online + text);
            }
            else
            {
                string text = "Stream is offline";
                client.SendMessage(channel, chatUser + " " + text);
            }
        }
        public async Task SelectAtRandom()
        {
            await Task.Run(new Action(() =>
            {
                int count = ActiveUsers.Organize();
                int rand = new Random().Next(count);
                string previous = selected;
                while (true)
                {
                    if (activeUsers[rand] != null)
                        selected = activeUsers[rand].chatUser;

                    rand = new Random().Next(count);
                    if (selected != previous)
                        break;
                }
                string text = "@" + selected + " please provide a " + verbType;
                client.SendMessage(channel, text);
            }));
        }

        private void CanSend(object sender, ElapsedEventArgs e)
        {
            CanInteract = true;
            CanWhisper = true;
        }

        private void FileStart(string path, WriteType type)
        {
            if (!File.Exists(path))
            {
                switch (type)
                {
                    case WriteType.Pref:
                        using (StreamWriter sw = new StreamWriter(path))
                            foreach (string s in userPref)
                                sw.Write(s + ";");
                        break;
                    case WriteType.Data:
                        using (StreamWriter sw = new StreamWriter(path))
                            sw.Write("\r");
                        break;
                    default:
                        break;
                }
            }
        }

        private Thread storyThread;
        private string
            connectErr = "Connection values either invalid or there is another problem" + "\n",
            setupMsg = "File system setup complete for first time use" + "\n",
            newCmd = "Command has been created" + "\n",
            editCmd = "Command has been modified" + "\n";
        private string verbInput;
        private string verbType;
        private string result;
        private string[] verbs = new string[]
        {
            "NOUN",
            "VERB",
            "ADJ",
            "ADVERB",
            "NAME",
            "PLACE",
            "COLOR"
        };
        private bool input = true;
        private bool begun;

        private void LogText(MessageType type)
        {
            /*
            switch (type)
            {
                case MessageType.Error:
                    Log.AppendText(connectErr);
                    break;
                case MessageType.Setup:
                    Log.AppendText(setupMsg);
                    break;
                case MessageType.NewCmd:
                    Log.AppendText(newCmd);
                    break;
                case MessageType.Edit:
                    Log.AppendText(editCmd);
                    break;
                default:
                    break;
            }
            */
        }

        private void UpdateList()
        {
            NameList.Items.Clear();

            var files = Directory.EnumerateFiles(botPath);
            foreach (string s in files)
            {
                if (s.EndsWith(".text"))
                {
                    string name = s.Substring(s.IndexOf('\\') + 1);
                    NameList.Items.Add(name.Substring(0, name.Length - 5));
                }
            }
        }

        #region Story functions
        private void Button_Interact_Click(object sender, RoutedEventArgs e)
        {
            string text = TextBox_Interact.Text;
            string name = Interact_Name.Text;
            char separator = ';';
            int count = 0;

            for (int i = 0; i < text.Length; i++)
            {
                foreach (string s in verbs)
                {
                    if (i == 0 || i <= Math.Max(text.Length - s.Length, s.Length - text.Length))
                        if (text.Substring(i, s.Length) == s)
                            count++;
                }
            }

            string path = botPath + name + ".text";
            using (StreamWriter sw = new StreamWriter(path))
                sw.Write(name + separator + count + separator + text);

            UpdateList();
        }
        private void Button_Check_Click(object sender, RoutedEventArgs e)
        {
            string name = Interact_Name.Text;
            string path = botPath + name + ".text";
            if (File.Exists(path))
            {
                using (StreamReader sr = new StreamReader(path))
                    TextBox_Interact.Text = sr.ReadToEnd().Split(';')[2];
            }
        }
        private void Begin_Click(object sender, RoutedEventArgs e)
        {
            begun = true;
            string name = Interact_Name.Text;
            string path = botPath + name + ".text";
            string story = string.Empty;
            bool init = false;
            int total = 0;

            if (File.Exists(path))
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    var lines = sr.ReadToEnd().Split(';');
                    int.TryParse(lines[1], out total);
                    story = lines[2];
                }
                result = string.Empty;

                string text = "Story entitled " + name + " has begun with " + total + " word entries";
                client.SendMessage(channel, text);

                if (storyThread != null)
                    storyThread.Abort();

                storyThread = new Thread(() =>
                {
                    int length = story.Length;
                    int count = 0;

                    while (count < length)
                    {
                        foreach (string s in verbs)
                        {
                            if ((count == 0 || count <= Math.Max(story.Length - s.Length, s.Length - story.Length)) && story.Substring(count, s.Length) == s)
                            { }
                                //Info.Block_Verbs.Text += s + ", ";
                        }
                        count++;
                    }

                    count = 0;
                    while (count < length)
                    {
                        //Info.Block_Work.Text = result;
                        
                        foreach (string s in verbs)
                        {
                            if ((count == 0 || count <= Math.Max(story.Length - s.Length, s.Length - story.Length)) && story.Substring(count, s.Length) == s)
                            {
                                input = false;
                                verbType = s;
                                count += s.Length;
                                break;
                            }
                        }
                        while (!input)
                        {
                            Thread.Sleep(300);
                        }
                        result += verbInput;
                        verbInput = string.Empty;

                        if (count < length)
                            result += story.Substring(count, 1);

                        count++;
                    }
                    begun = false;
                    var tts = new SpeechSynthesizer();
                    tts.Speak(result);
                    tts.Dispose();
                //  TextBox_Interact.Text = result;
                });
                storyThread.SetApartmentState(ApartmentState.STA);
                storyThread.Start();
            }
        }
        private void Button_Input_Click(object sender, RoutedEventArgs e)
        {
            verbInput = Interact_Name.Text;
            input = true;
        }
        #endregion

        private void Button_Prefer_Click(object sender, RoutedEventArgs e)
        {
            var window = new Preferences();
            window.Show();
        }

        private void On_ViewClick(object sender, RoutedEventArgs e)
        {
            ViewChat = true;
        }

        private void Select_Story(object sender, SelectionChangedEventArgs e)
        {
            if (NameList.SelectedItem != null)
            {
                string name = NameList.SelectedItem.ToString();
                string path = botPath + name + ".text";

                if (File.Exists(path))
                {
                    using (StreamReader sr = new StreamReader(path))
                        TextBox_Interact.Text = sr.ReadToEnd().Split(';')[2];
                }
                Interact_Name.Text = name;
            }
        }

        public void WriteJoinData(string path, string file)
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
                        "<date> :" + date   + ";",
                        "<first> :" + date  + ";",
                        "<sub> : false"     + ";",
                        "<permit> :false"   + ";",
                        "<count> :1"        + ";",
                        "<currency> :0"     + ";",
                        "<warning> :0"
                    };

                    foreach (string s in lines)
                        sw.Write(s + sw.NewLine);
                }
            }
            else
            {
                string oldDate = default(string);
                string[] lines;
                using (StreamReader sr = new StreamReader(path + file))
                {
                    lines = sr.ReadToEnd().Split(';');
                    string s = string.Empty;
                    foreach (string select in lines)
                    {
                        if (select.StartsWith("<date>"))
                        {
                            s = select;
                            break;
                        }
                    }
                    oldDate = s.Substring(s.IndexOf(':') + 1);
                }
                if (date != oldDate)
                {
                    WriteUserData(path, file, string.Empty, DataType.Joined);
                }
            }
        }

        public void WriteUserData(string path, string file, string chatUser, DataType type)
        {
            string date = DateTime.Today.ToShortDateString();
            string[] lines;
            string[] ids = new string[]
            {
                "date",
                "first",
                "sub",
                "permit",
                "count",
                "currency",
                "warning"
            };

            using (StreamReader sr = new StreamReader(path + file))
                lines = sr.ReadToEnd().Split(';');
            using (StreamWriter sw = new StreamWriter(path + file))
            {
                if (type == DataType.Joined)
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
                }
                else if (type == DataType.Permit || type == DataType.NewSub)
                {
                    string input = string.Empty;
                    switch (type)
                    {
                        case DataType.Permit:
                            input = "<permit>";
                            break;
                        case DataType.NewSub:
                            input = "<sub>";
                            break;
                        default:
                            break;
                    }
                    for (int i = 0; i < lines.Length; i++)
                    {
                        bool allow;
                        if (lines[i].Contains(input))
                        {
                            bool.TryParse(lines[i].Substring(lines[i].IndexOf(':') + 1), out allow);
                            allow = !allow;
                            lines[i] = input + " :" + allow;

                            if (type == DataType.Permit)
                            {
                                string differ = allow ? "yes" : "no";
                                string text = "@" + chatUser + " permissions changed to: " + differ;
                                client.SendMessage(channel, text);
                            }
                            else if (type == DataType.NewSub)
                            {
                                string text = "Welcome " + chatUser + " to the sub group!";
                                client.SendMessage(channel, text);
                            }
                            break;
                        }
                    }
                }
                else if (type == DataType.Warning)
                {
                    int count = 0;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains("<warning>"))
                        {
                            int.TryParse(lines[i].Substring(lines[i].IndexOf(':') + 1), out count);
                            lines[i] = "<warning> :" + (count + 1);
                        }
                    }
                    if (count == 10)
                    {
                        var time = new TimeSpan(0, 0, 10);
                        client.TimeoutUser(chatUser, time, "You've reached the warning threshold, try to be more careful");

                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Contains("<warning>"))
                                lines[i] = "<warning> :0";
                        }
                    }
                }
                foreach (string s in lines)
                {
                    foreach (string id in ids)
                    {
                        if (s.Contains(id) && id != "warning")
                        {
                            if (s.Contains("\r\n"))
                                sw.Write(s.Substring(2) + ";" + sw.NewLine);
                            else
                                sw.Write(s + ";" + sw.NewLine);
                            break;
                        }
                        if (id == "warning" && type != DataType.Warning)
                        {
                            sw.Write(s.Substring(2));
                            break;
                        }
                        if (id == "warning" && type == DataType.Warning)
                        {
                            sw.Write(s);
                            break;
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

            string command = "!" + name + " " + message;
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
                        break;
                    }
                    if (line.Length < 5)
                    {
                        lines[i] = command;
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
        public string MessageOutput(string file, char separator, string name, string user)
        {
            string[] lines;
            using (StreamReader sr = new StreamReader(file))
                lines = sr.ReadToEnd().Split(separator);
            foreach (string s in lines)
            {
                if (s.Contains(name))
                {
                    if (s.Contains("$user"))
                    {
                        string insert = "$user";
                        string text = s.Insert(s.IndexOf(insert), user);
                        text = text.Substring(0, text.IndexOf(insert)) + text.Substring(text.IndexOf(insert) + insert.Length);
                        if (text.Contains("\n"))
                            return text.Substring(text.IndexOf('\n') + name.Length + 2);
                        else
                            return text.Substring(name.Length + 1);
                    }
                    else
                    {
                        if (s.Contains("\n"))
                            return s.Substring(s.IndexOf('\n') + name.Length + 2);
                        else
                            return s.Substring(name.Length + 1);
                    }
                }
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
    public enum DataType
    {
        Joined,
        Permit,
        Warning,
        NewSub
    }
    public enum WriteType
    {
        Pref,
        Data
    }

    public class TimedMessage : IDisposable
    {
        public bool active;
        public string name;
        public string message;
        public int frequency;
        public int chance;
        public int ID;
        private System.Timers.Timer schedule;
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
            foreach (TimedMessage task in MainWindow.TimedMsgs)
            {
                if (task != null && task.name == name)
                {
                    num = task.ID;
                    task.Dispose();

                    string text = "Task ID: " + task.ID + " replaced";
                    MainWindow.client.SendMessage(Base.channel, text);
                    break;
                }
            }
            MainWindow.TimedMsgs[num] = new TimedMessage();
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
            schedule = new System.Timers.Timer();
            schedule.Interval = frequency * 60000;
            schedule.Elapsed += SendMessage;
            schedule.Start();
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

    public class ActiveUsers : IDisposable
    {
        public string chatUser;
        public bool active;
        public int ID;
        public int seconds;
        private System.Timers.Timer timer;
        public static int NewUser(string user, int interval)
        {
            int num = 1001;
            var users = MainWindow.activeUsers;
            foreach (ActiveUsers active in users)
            {
                if (active != null && active.chatUser == user)
                {
                    active.Activate();
                    return num;
                }
            }
            for (int i = 0; i < users.Length; i++)
            {
                if (users[i] == null || !users[i].active)
                {
                    num = i;
                    break;
                }
            }
            MainWindow.activeUsers[num] = new ActiveUsers();
            MainWindow.activeUsers[num].chatUser = user;
            MainWindow.activeUsers[num].active = true;
            MainWindow.activeUsers[num].ID = num;
            MainWindow.activeUsers[num].seconds = interval;
            MainWindow.activeUsers[num].Activate();
            return num;
        }
        public void Dispose()
        {
            MainWindow.activeUsers[ID].active = false;
            MainWindow.activeUsers[ID] = null;
        }
        public static int Organize()
        {
            int count = 0;
            var users = MainWindow.activeUsers;
            foreach (ActiveUsers user in users)
            {
                if (user != null)
                {
                    int num = Math.Max(user.ID - 1, 0);
                    if (users[num] == null)
                    {
                        users[num] = users[user.ID];
                        users[user.ID].Dispose();
                    }
                    count++;
                }
            }
            MainWindow.activeUsers = users;
            return count;
        }
        private void Activate()
        {
            if (timer == null)
            {
                timer = new System.Timers.Timer();
                timer.Interval = 1000 * seconds;
                timer.Start();
                timer.Elapsed += Cycle;
            }
            else
            {
                timer.Stop();
                timer.Dispose();
                timer = new System.Timers.Timer();
                timer.Interval = 1000 * seconds;
                timer.Start();
                timer.Elapsed += Cycle;
            }
        }
        private void Cycle(object sender, ElapsedEventArgs e)
        {
            timer.Stop();
            timer.Dispose();
            MainWindow.activeUsers[ID].Dispose();
        }
    }
}
