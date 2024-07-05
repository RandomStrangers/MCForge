/*
	Copyright © 2009-2014 MCSharp team (Modified for use with MCZall/MCLawl/MCForge/MCForge-Redux)
	
	Dual-licensed under the	Educational Community License, Version 2.0 and
	the GNU General Public License, Version 3 (the "Licenses"); you may
	not use this file except in compliance with the Licenses. You may
	obtain a copy of the Licenses at
	
	http://www.opensource.org/licenses/ecl2.php
	http://www.gnu.org/licenses/gpl-3.0.html
	
	Unless required by applicable law or agreed to in writing,
	software distributed under the Licenses are distributed on an "AS IS"
	BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
	or implied. See the Licenses for the specific language governing
	permissions and limitations under the Licenses.
*/
using System;
using System.Windows.Forms;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MCForge;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net;
using System.Reflection;


namespace MCForge_.Gui
{
    public static class Program
    {
        public static DateTime startTime;
        public static bool usingConsole = false;
        public static string parent = Path.GetFileName(Assembly.GetEntryAssembly().Location);
        public static string parentfullpath = Assembly.GetEntryAssembly().Location;
        public static string parentfullpathdir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static string CurrentVersionFile = "https://github.com/RandomStrangers/MCForge/raw/master/Uploads/current_version.txt";
        private static string DLLLocation = "https://github.com/RandomStrangers/MCForge/raw/master/Uploads/MCForge_.dll";
        private static string ChangelogLocation = "https://github.com/RandomStrangers/MCForge/raw/master/Uploads/changelog.txt";
        private static string EXELocation = "https://github.com/RandomStrangers/MCForge/raw/master/Uploads/MCForge.exe";
        private static string CLIEXELocation = "https://github.com/RandomStrangers/MCForge/raw/master/Uploads/MCForgeCLI.exe";
        private static string RevisionList = "https://github.com/RandomStrangers/MCForge/raw/master/revs.txt";

        /* [DllImport("kernel32")]
         public static extern IntPtr GetConsoleWindow();
         [DllImport("user32.dll")]
         public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);*/
        public static void GlobalExHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Server.ErrorLog(ex);
            Thread.Sleep(500);

            if (Server.restartOnError)
                ExitProgram(true);
        }

        public static void ThreadExHandler(object sender, ThreadExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            Server.ErrorLog(ex);
            Thread.Sleep(500);

            if (Server.restartOnError)
                ExitProgram(true);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            startTime = DateTime.Now;
            if (Process.GetProcessesByName("MCForge").Length != 1)
            {
                foreach (Process pr in Process.GetProcessesByName("MCForge"))
                {
                    if (pr.MainModule.BaseAddress == Process.GetCurrentProcess().MainModule.BaseAddress)
                        if (pr.Id != Process.GetCurrentProcess().Id)
                            pr.Kill();
                }
            }
            PidgeonLogger.Init();
            AppDomain.CurrentDomain.UnhandledException += GlobalExHandler;
            Application.ThreadException += ThreadExHandler;
            bool skip = false;
        remake:
            try
            {
                if (!File.Exists("Viewmode.cfg") || skip)
                {
                    StreamWriter SW = new StreamWriter(File.Create("Viewmode.cfg"));
                    SW.WriteLine("#This file controls how the console window is shown to the server host");
                    SW.WriteLine("#cli: True or False (Determines whether a CLI interface is used) (Set True if on Mono)");
                    SW.WriteLine("#high-quality: True or false (Determines whether the GUI interface uses higher quality objects)");
                    SW.WriteLine();
                    SW.WriteLine("cli = false");
                    SW.WriteLine("high-quality = true");
                    SW.Flush();
                    SW.Close();
                    SW.Dispose();
                }

                if (File.ReadAllText("Viewmode.cfg") == "") { skip = true; goto remake; }

                string[] foundView = File.ReadAllLines("Viewmode.cfg");
                if (foundView[0][0] != '#') { skip = true; goto remake; }

                if (args.Length == 0)
                {

                    /*IntPtr hConsole = GetConsoleWindow();
                    if (IntPtr.Zero != hConsole)
                    {
                        ShowWindow(hConsole, 0);
                    }*/
                    if (foundView[5].Split(' ')[2].ToLower() == "true")
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                    }

                    Application.Run(new MCForge.Gui.Window());
                }
                if (args[0].Contains("cli"))
                {
                    Server s = new Server();
                    s.OnLog += WriteToConsole;
                    s.OnCommand += WriteToConsole;
                    s.OnSystem += WriteToConsole;
                    s.Start();

                    Console.Title = Server.name + " - MCForge " + Server.Version;
                    usingConsole = true;
                    handleComm();


                    //Application.Run();
                }
                WriteToConsole("Completed in " + (DateTime.Now - startTime).Milliseconds + "ms");
            }
            catch (Exception e) { Server.ErrorLog(e); }
        }

        //U WANT TO SEE HOW WE DO IT MCSTORM?
        //I WOULD EXPLAIN, BUT ITS BETTER TO JUST COPY AND PASTE, AMIRITE?
        private static void WriteToConsole(string message)
        {
            if (!message.Contains("&") && !message.Contains("%"))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message);
                return;
            }
            string[] splitted = message.Split('&', '%');
            for (int i = 0; i < splitted.Length; i++)
            {
                string elString = splitted[i];
                if (String.IsNullOrEmpty(elString))
                    continue;
                Console.ForegroundColor = GetColor(elString[0]);
                Console.Write(elString.Substring(1));
                if (i != splitted.Length - 1)
                    continue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write('\n');
            }
        }

        private static ConsoleColor GetColor(char c)
        {
            switch (c)
            {
                case '0': return ConsoleColor.DarkGray;
                case '1': return ConsoleColor.DarkBlue;
                case '2': return ConsoleColor.DarkGreen;
                case '3': return ConsoleColor.DarkCyan;
                case '4': return ConsoleColor.DarkMagenta;
                case '7': return ConsoleColor.Gray;
                case '5': return ConsoleColor.Magenta;
                case '6': return ConsoleColor.DarkYellow;
                case '8': return ConsoleColor.DarkGray;
                case '9': return ConsoleColor.Blue;
                case 'a': return ConsoleColor.Green;
                case 'b': return ConsoleColor.Cyan;
                case 'c': return ConsoleColor.Red;
                case 'd': return ConsoleColor.Magenta;
                case 'e': return ConsoleColor.Yellow;
                //Dont need f, it will default to white.
                default:
                    return ConsoleColor.White;
            }
        }

        public static void handleComm()
        {
            string s, msg;
            while (true)
            {
                try
                {
                    string sentCmd = String.Empty, sentMsg = String.Empty;
                    s = Console.ReadLine().Trim(); // Make sure we have no whitespace!

                    if (s.Length < 1) continue;
                    if (s[0] == '/') s = s.Remove(0, 1);
                    else goto talk;
                    if (s.IndexOf(' ') != -1)
                    {
                        sentCmd = s.Substring(0, s.IndexOf(' '));
                        sentMsg = s.Substring(s.IndexOf(' ') + 1);
                    }
                    else if (s != String.Empty) sentCmd = s;
                    else goto talk;

                    try
                    {
                        if (Server.Check(sentCmd, sentMsg)) { Server.cancelcommand = false; continue; }
                        Command cmd = Command.all.Find(sentCmd);
                        if (cmd != null)
                        {
                            if (Player.CommandProtected(sentCmd, sentMsg)) { continue; }
                            cmd.Use(null, sentMsg);
                            Console.WriteLine("CONSOLE: USED /" + sentCmd + " " + sentMsg);
                            if (sentCmd.ToLower() != "restart")
                                continue;
                            break;
                        }
                        else
                        {
                            Console.WriteLine("CONSOLE: Unknown command.");
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        Server.ErrorLog(e);
                        Console.WriteLine("CONSOLE: Failed command.");
                        continue;
                    }

                talk:
                    if (s[0] == '@')
                    {
                        s = s.Remove(0, 1);
                        int spacePos = s.IndexOf(' ');
                        if (spacePos == -1) { Console.WriteLine("No message entered."); continue; }
                        Player pl = Player.Find(s.Substring(0, spacePos));
                        if (pl == null) { Console.WriteLine("Player not found."); continue; }
                        msg = String.Format("&9[>] {0}Console [&a{1}{0}]: &f{2}", Server.DefaultColor, Server.ZallState, s.Substring(spacePos + 1));
                        Player.SendMessage(pl, msg);
                    }
                    else if (s[0] == '#')
                    {
                        msg = String.Format("To Ops -{0}Console [&a{1}{0}]&f- {2}", Server.DefaultColor, Server.ZallState, s);
                        Player.GlobalMessageOps(msg);
                        Server.IRC.Say(msg, true);
                    }
                    else if (s[0] == '+')
                    {
                        msg = String.Format("To Admins -{0}Console [&a{1}{0}]&f- {2}", Server.DefaultColor, Server.ZallState, s);
                        Player.GlobalMessageAdmins(msg);
                        Server.IRC.Say(msg, true);
                    }
                    else
                    {
                        msg = String.Format("{0}Console [&a{1}{0}]: &f{2}", Server.DefaultColor, Server.ZallState, s);
                        Player.GlobalMessage(msg);
                        Server.IRC.Say(msg);
                    }
                    WriteToConsole(msg);
                }
                catch (Exception ex)
                {
                    Server.ErrorLog(ex);
                }
            }
        }
        /*
        public static void handleComm(string s)
        {

            string sentCmd = "", sentMsg = "";
            List<string> cmdtest = new List<string>();
            cmdtest = Command.all.commandNames();
            //blank lines are considered accidental
            if (s == "")
            {
                handleComm(Console.ReadLine());
                return;
            }

            //commands all start with a slash

            if (s.IndexOf('/') == 0)
            {
                //remove the preceding slash
                s = s.Remove(0, 1);

                //continue parsing
                if (s.IndexOf(' ') != -1)
                {
                    sentCmd = s.Split(' ')[0];
                    sentMsg = s.Substring(s.IndexOf(' ') + 1);
                }
                else if (s != "")
                {
                    sentCmd = s;
                }
            }
            //anything else is treated as chat
            else
            {
                sentCmd = "say";
                sentMsg = Server.DefaultColor + "Console [&a" + Server.ZallState + Server.DefaultColor + "]: &f" + s;
            }



            try
            {

                Command cmd = Command.all.Find(sentCmd);
                if (cmd != null)
                {
                    cmd.Use(null, sentMsg);
                    Console.WriteLine("CONSOLE: USED /" + sentCmd + " " + sentMsg);
                    handleComm(Console.ReadLine());
                    return;
                }


            }
            catch (Exception e)
            {
                Server.ErrorLog(e);
                Console.WriteLine("CONSOLE: Failed command.");
                handleComm(Console.ReadLine());
                return;
            }
            //handleComm(Console.ReadLine());


        } */

        public static System.Timers.Timer updateTimer = new System.Timers.Timer(120 * 60 * 1000);

        static public void ExitProgram(bool AutoRestart)
        {
            Server.restarting = AutoRestart;
            Server.shuttingDown = true;

            Server.Exit(AutoRestart);

            new Thread(new ThreadStart(delegate
            {
                /*try
                {
                    if (MCForge.Gui.Window.thisWindow.notifyIcon1 != null)
                    {
                        MCForge.Gui.Window.thisWindow.notifyIcon1.Icon = null;
                        MCForge.Gui.Window.thisWindow.notifyIcon1.Visible = false;
                        MCForge.Gui.Window.thisWindow.notifyIcon1.Dispose();
                    }
                }
                catch { }
                */
                if (AutoRestart)
                {
                    saveAll(true);

                    if (Server.listen != null) Server.listen.Close();
                    if (!usingConsole)
                    {
                        Process.Start(parent);
                        Environment.Exit(0);
                    }
                    else
                    {
                        //Process.Start("mono", parentfullpath);
                        Application.Exit();
                        Application.Restart();
                    }
                }
                else
                {
                    saveAll(false);
                    Application.Exit();
                    if (usingConsole)
                    {
                        Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                    }
                    Environment.Exit(0);
                }
            })).Start();
        }

        static public void saveAll(bool restarting)
        {
            try
            {
                List<Player> kickList = new List<Player>();
                kickList.AddRange(Player.players);
                foreach (Player p in kickList)
                {
                    if (restarting)
                        p.Kick("Server restarted! Rejoin!");
                    else
                        p.Kick("Server is shutting down.");
                }
            }
            catch (Exception exc) { Server.ErrorLog(exc); }

            try
            {
                string level = null;
                foreach (Level l in Server.levels)
                {
                    if (!Server.lava.active || !Server.lava.HasMap(l.name))
                    {
                        level = level + l.name + "=" + l.physics + System.Environment.NewLine;
                        l.Save(false, true);
                    }
                    l.saveChanges();
                }
                if (Server.ServerSetupFinished && !Server.AutoLoad)
                {
                    File.WriteAllText("text/autoload.txt", level);
                }
            }
            catch (Exception exc) { Server.ErrorLog(exc); }
        }
    }
}

