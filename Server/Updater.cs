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
using System.IO;
using System.Windows.Forms;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net;
using System.Reflection;
using MCForge;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using static MCForge.Player;

namespace MCForge_.Gui
{
    public static class Updater
    {
        public static bool usingConsole = false;
        public static string parent = Path.GetFileName(Assembly.GetEntryAssembly().Location);
        public static string parentfullpath = Assembly.GetEntryAssembly().Location;
        public static string parentfullpathdir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public const string BaseURL = "https://github.com/RandomStrangers/MCForge/blob/master/";
        public const string UploadsURL = "https://github.com/RandomStrangers/MCForge/raw/master/Uploads/";
        const string CurrentVersionURL = UploadsURL + "current_version.txt";
        const string dllURL = UploadsURL + "MCForge_.dll";
        // I'll leave it here just in case I decide to update the changelog, though highly not likely.
        // const string changelogURL = UploadsURL + "Changelog.txt";
        const string guiURL = UploadsURL + "MCForge.exe";
        const string cliURL = UploadsURL + "MCForgeCLI.exe";

        public static event EventHandler NewerVersionDetected;
        static void UpdateCheck()
        {
            if (!Server.autoupdate) return;
            WebClient Client = new WebClient();

            try
            {
                string latest = Client.DownloadString(CurrentVersionURL);

                if (new Version(Server.Version) >= new Version(latest))
                {
                    Server.s.Log("No update found!");
                }
                else if (NewerVersionDetected != null)
                {
                    NewerVersionDetected(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Server.s.Log("Error checking for updates");
            }
            Client.Dispose();
        }
        public static void UpdateCheck(bool wait = false, Player p = null)
        {
            WebClient Client = new WebClient();

            string latest1 = Client.DownloadString(CurrentVersionURL);
            if (new Version(Server.Version) >= new Version(latest1))
            {
                if (Server.autoupdate == true || p != null)
                {
                    if (Server.autonotify == true || p != null)
                    {
                        //if (p != null) Server.restartcountdown = "20";  This is set by the user.  Why change it?
                        Player.GlobalMessage("Update found. Prepare for restart in &f" + Server.restartcountdown + Server.DefaultColor + " seconds.");
                        Server.s.Log("Update found. Prepare for restart in " + Server.restartcountdown + " seconds.");
                        //               double nxtTime = Convert.ToDouble(Server.restartcountdown);
                        //                         DateTime nextupdate = DateTime.Now.AddMinutes(nxtTime);
                        int timeLeft = Convert.ToInt32(Server.restartcountdown);
                        System.Timers.Timer countDown = new System.Timers.Timer();
                        countDown.Interval = 1000;
                        countDown.Start();
                        countDown.Elapsed += delegate
                        {
                            if (Server.autoupdate == true || p != null)
                            {
                                Player.GlobalMessage("Updating in &f" + timeLeft + Server.DefaultColor + " seconds.");
                                Server.s.Log("Updating in " + timeLeft + " seconds.");
                                timeLeft = timeLeft - 1;
                                if (timeLeft < 0)
                                {
                                    Player.GlobalMessage("---UPDATING SERVER---");
                                    Server.s.Log("---UPDATING SERVER---");
                                    countDown.Stop();
                                    countDown.Dispose();

                                }
                            }
                            else
                            {
                                PerformUpdate();
                            }
                        };
                    }
                    void PerformUpdate()
                    {
                        try
                        {
                            //StreamWriter SW;
                            //if (!Server.mono)
                            //{
                            //    if (!File.Exists("Update.bat"))
                            //        SW = new StreamWriter(File.Create("Update.bat"));
                            //    else
                            //    {
                            //        if (File.ReadAllLines("Update.bat")[0] != "::Version 3")
                            //        {
                            //            SW = new StreamWriter(File.Create("Update.bat"));
                            //        }
                            //        else
                            //        {
                            //            SW = new StreamWriter(File.Create("Update_generated.bat"));
                            //        }
                            //    }
                            //    SW.WriteLine("::Version 3");
                            //    SW.WriteLine("TASKKILL /pid %2 /F");
                            //    SW.WriteLine("if exist MCForge_.dll.backup (erase MCForge_.dll.backup)");
                            //    SW.WriteLine("if exist MCForge_.dll (rename MCForge_.dll MCForge_.dll.backup)");
                            //    SW.WriteLine("if exist MCForge.new (rename MCForge.new MCForge_.dll)");
                            //    SW.WriteLine("start MCForge.exe");
                            //}
                            //else
                            //{
                            //    if (!File.Exists("Update.sh"))
                            //        SW = new StreamWriter(File.Create("Update.sh"));
                            //    else
                            //    {
                            //        if (File.ReadAllLines("Update.sh")[0] != "#Version 2")
                            //        {
                            //            SW = new StreamWriter(File.Create("Update.sh"));
                            //        }
                            //        else
                            //        {
                            //            SW = new StreamWriter(File.Create("Update_generated.sh"));
                            //        }
                            //    }
                            //    SW.WriteLine("#Version 2");
                            //    SW.WriteLine("#!/bin/bash");
                            //    SW.WriteLine("kill $2");
                            //    SW.WriteLine("rm MCForge_.dll.backup");
                            //    SW.WriteLine("mv MCForge_.dll MCForge.dll_.backup");
                            //    SW.WriteLine("wget " + DLLLocation);
                            //    SW.WriteLine("mono MCForge.exe");
                            //}

                            //SW.Flush(); SW.Close(); SW.Dispose();

                            //Process proc = Process.GetCurrentProcess();
                            //string assemblyname = proc.ProcessName + ".exe";

                            //WebClient client = new WebClient();
                            //Server.selectedrevision = client.DownloadString(Program.CurrentVersionFile);
                            //client.Dispose();

                            //string verscheck = Server.selectedrevision.TrimStart('r');
                            //int vers = int.Parse(verscheck.Split('.')[0]);
                            try
                            {
                                if (File.Exists("MCLawl.new"))
                                    File.Delete("MCLawl.new");
                                if (File.Exists("Changelog.txt"))
                                    File.Delete("Changelog.txt");
                                if (File.Exists("MCForge_.update"))
                                    File.Delete("MCForge_.update");
                                if (File.Exists("MCForge.update"))
                                    File.Delete("MCForge.update");
                                if (File.Exists("MCForgeCLI.update"))
                                    File.Delete("MCForgeCLI.update");
                                if (File.Exists("Update.bat"))
                                    File.Delete("Update.bat");
                                if (File.Exists("Update_generated.bat"))
                                    File.Delete("Update_generated.bat");
                                if (File.Exists("Update.sh"))
                                    File.Delete("Update.sh");
                                if (File.Exists("Update_generated.sh"))
                                    File.Delete("Update_generated.sh");
                            }
                            catch { }
                            WebClient Client1 = new WebClient();
                            Client1.DownloadString(dllURL);
                            Client1.DownloadString(guiURL);
                            Client1.DownloadString(cliURL);
                            // I'll leave it here just in case I decide to update the changelog, though highly not likely.
                            // Client1.DownloadString(changelogURL);

                            // Its possible there are no levels or players loaded yet
                            // Only save them if they exist, otherwise we fail-whale
                            if (Server.levels != null && Server.levels.Any())
                                foreach (Level l in Server.levels)
                                    if (Server.lava.active && Server.lava.HasMap(l.name)) l.saveChanges();
                                    else l.Save();

                            if (Player.players != null && Player.players.Any())
                                foreach (Player pl in Player.players) pl.save();

                            //File.WriteAllBytes("Updater.exe", MCForge.Properties.Resources.Updater);
                            if (!usingConsole)
                                Process.Start("Updater.exe", "securitycheck10934579068013978427893755755270374" + parent);
                            else
                            {
                                Process.Start("mono", parentfullpathdir + "/Updater.exe securitycheck10934579068013978427893755755270374" + parent);
                            }
                            ExitProgram(false);
                        }
                        catch (Exception e) { Server.ErrorLog(e); }
                    }

                    void ExitProgram(bool AutoRestart)
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

                    void saveAll(bool restarting)
                    {
                        try
                        {
                            List<Player> kickList = new List<Player>();
                            kickList.AddRange(Player.players);
                            foreach (Player p1 in kickList)
                            {
                                if (restarting)
                                    p1.Kick("Server restarted! Rejoin!");
                                else
                                    p1.Kick("Server is shutting down.");
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
                /// <summary>
                /// Loads updater properties from given file
                /// </summary>
                /// <param name="givenPath">File path relative to server to load properties from</param>
                void Load(string givenPath)
                {
                    if (File.Exists(givenPath))
                    {
                        string[] lines = File.ReadAllLines(givenPath);

                        foreach (string line in lines)
                        {
                            if (line != "" && line[0] != '#')
                            {
                                string key = line.Split('=')[0].Trim();
                                string value = line.Split('=')[1].Trim();

                                switch (key.ToLower())
                                {
                                    case "autoupdate":
                                        Server.autoupdate = (value.ToLower() == "true") ? true : false;
                                        break;
                                    case "notify":
                                        Server.notifyPlayers = (value.ToLower() == "true") ? true : false;
                                        break;
                                    case "restartcountdown":
                                        Server.restartcountdown = value;
                                        break;

                                }
                            }
                        }
                    }
                }

            }
        }
        public static void Load(string givenPath)
        {
            if (File.Exists(givenPath))
            {
                string[] lines = File.ReadAllLines(givenPath);

                foreach (string line in lines)
                {
                    if (line != "" && line[0] != '#')
                    {
                        string key = line.Split('=')[0].Trim();
                        string value = line.Split('=')[1].Trim();

                        switch (key.ToLower())
                        {
                            case "autoupdate":
                                Server.autoupdate = (value.ToLower() == "true") ? true : false;
                                break;
                            case "notify":
                                Server.notifyPlayers = (value.ToLower() == "true") ? true : false;
                                break;
                            case "restartcountdown":
                                Server.restartcountdown = value;
                                break;

                        }
                    }
                }
            }
        }

    }
}
