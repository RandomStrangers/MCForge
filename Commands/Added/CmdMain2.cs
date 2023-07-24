/*
    Copyright 2011 MCForge
        
    Dual-licensed under the Educational Community License, Version 2.0 and
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

namespace MCForge.Commands
{
    public class CmdMain2 : Command
    {
        public override string name { get { return "setmain"; } }
        public override string shortcut { get { return "seth"; } }
        public override string type { get { return "other"; } }
        public override bool museumUsable { get { return true; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override CommandAlias[] Aliases
        {
            get { return new[] { new CommandAlias("main2"), new CommandAlias("SetWorldMain") }; }
        }

        public override void Use(Player p, string message)
    /*    {
            if (message.Length == 0)
            {
                if (p != null)
                {
                    all.Find("goto").Use(p, Server.mainLevel.name);
                }
                else
                {
                    Player.SendMessage(p, "Main level is " + Server.mainLevel.name);
                }
            }
            else */
            {
                string map = Matcher.FindMaps(p, message);
                if (map == null) return;
                if (Server.mainLevel != p.level)
                {
                    Server.mainLevel = p.level;
                    SrvProperties.Save("properties/main.properties");
                    Player.SendMessage(p, "Set main level to " + Server.mainLevel.name);
                }
            }
      //  }
        
    
        public override void Help(Player p)
        {
            p.SendMessage("&T/SetMain");
            p.SendMessage("&HSends you to the main level.");
            p.SendMessage("&T/SetMain [level]");
            p.SendMessage("&HSets the main level to that level.");
        }
    }
}
