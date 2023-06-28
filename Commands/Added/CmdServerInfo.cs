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
using System.Diagnostics;
namespace MCForge.Commands
{
    public class CmdServerInfo : Command
    {
        public override string name { get { return "serverinfo"; } }
        public override string shortcut { get { return "host"; } }
        public override string type { get { return "information"; } }
        public override bool museumUsable { get { return true; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }
        public CmdServerInfo() { }
        public override CommandAlias[] Aliases
        {
            get { return new[] { new CommandAlias("sinfo") }; }
        }
        public override void Use(Player p, string message)
        {
            if (p == null)
            {
                if (Server.PCCounter == null)
                    Player.SendMessage(p, "");
                Player.SendMessage(p, "About &5" + Server.name + ":");
                    Player.SendMessage(p, " Owner is &b" + Server.Owner);
                Player.SendMessage(p, "Running " + Server.SoftwareNameVersioned + ", " + Server.SourceURL);
                Player.SendMessage(p, "Console name: &3" + Server.ZallState);
                Player.SendMessage(p, "Starting performance counters...one second");
                Server.PCCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                Server.PCCounter.BeginInit();
                Server.PCCounter.NextValue();
                if (Server.ProcessCounter == null)
                {
                    Server.ProcessCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                    Server.ProcessCounter.BeginInit();
                    Server.ProcessCounter.NextValue();
                }


                //   TimeSpan tp = Process.GetCurrentProcess().TotalProcessorTime;
                TimeSpan up = (DateTime.Now - Process.GetCurrentProcess().StartTime);

                //To get actual CPU% is OS dependant
                string ProcessorUsage = "CPU Usage (Processes : All Processes):" + Server.ProcessCounter.NextValue() + " : " + Server.PCCounter.NextValue();
                //Alternative Average?
                //string ProcessorUsage = "CPU Usage is Not Implemented: So here is ProcessUsageTime/ProcessTotalTime:"+String.Format("00.00",(((tp.Ticks/up.Ticks))*100))+"%";
                //reports Private Bytes because it is what the process has reserved for itself and is unsharable
                string MemoryUsage = "Memory Usage: " + Math.Round((double)Process.GetCurrentProcess().PrivateMemorySize64 / 1048576).ToString() + " Megabytes";
                string Uptime = "Uptime: " + up.Days + " Days " + up.Hours + " Hours " + up.Minutes + " Minutes " + up.Seconds + " Seconds";
                string Threads = "Threads: " + Process.GetCurrentProcess().Threads.Count;
                Player.SendMessage(p, Uptime);
                Player.SendMessage(p, MemoryUsage);
                Player.SendMessage(p, ProcessorUsage);
                Player.SendMessage(p, Threads);
                return;
            }
                if (p.group.Permission < LevelPermission.Operator)
                Player.SendMessage(p, "");
            Player.SendMessage(p, "About &5" + Server.name + ":");
            Player.SendMessage(p, " Owner is &b" + Server.Owner);
            Player.SendMessage(p, "Running " + Server.SoftwareNameVersioned + ", " + Server.SourceURL);
            Player.SendMessage(p, "Console name: &3" + Server.ZallState);
            p.cancelcommand = true;
            if (p.group.Permission >= LevelPermission.Operator)
            {
                if (Server.PCCounter == null)
                Player.SendMessage(p, "Starting performance counters...one second");
                Server.PCCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                Server.PCCounter.BeginInit();
                Server.PCCounter.NextValue();
                if (Server.ProcessCounter == null)
                {
                    Server.ProcessCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                    Server.ProcessCounter.BeginInit();
                    Server.ProcessCounter.NextValue();
                }


                //   TimeSpan tp = Process.GetCurrentProcess().TotalProcessorTime;
                TimeSpan up = (DateTime.Now - Process.GetCurrentProcess().StartTime);

                //To get actual CPU% is OS dependant
                string ProcessorUsage = "CPU Usage (Processes : All Processes):" + Server.ProcessCounter.NextValue() + " : " + Server.PCCounter.NextValue();
                //Alternative Average?
                //string ProcessorUsage = "CPU Usage is Not Implemented: So here is ProcessUsageTime/ProcessTotalTime:"+String.Format("00.00",(((tp.Ticks/up.Ticks))*100))+"%";
                //reports Private Bytes because it is what the process has reserved for itself and is unsharable
                string MemoryUsage = "Memory Usage: " + Math.Round((double)Process.GetCurrentProcess().PrivateMemorySize64 / 1048576).ToString() + " Megabytes";
                string Uptime = "Uptime: " + up.Days + " Days " + up.Hours + " Hours " + up.Minutes + " Minutes " + up.Seconds + " Seconds";
                string Threads = "Threads: " + Process.GetCurrentProcess().Threads.Count;
                Player.SendMessage(p, Uptime);
                Player.SendMessage(p, MemoryUsage);
                Player.SendMessage(p, ProcessorUsage);
                Player.SendMessage(p, Threads);
            }

        }
        public override void Help(Player p)
        {
            Player.SendMessage(p, "/serverinfo - Get server CPU%, RAM usage, Console name, Owner, Software, and uptime.");
        }
    }
}
