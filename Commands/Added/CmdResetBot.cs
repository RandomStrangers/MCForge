/*
	Copyright � 2009-2014 MCSharp team (Modified for use with MCZall/MCLawl/MCForge/MCForge-Redux)
	
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

namespace MCForge.Commands
{
    public class CmdResetBot : Command
    {
        public override string name { get { return "resetbot"; } }
        public override string shortcut { get { return  "resetirc"; } }
        public override string type { get { return "mod"; } }
        public override bool museumUsable { get { return true; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public CmdResetBot() { }

        public override void Use(Player p, string message)
        {
            Server.IRC.Reset();
        }
        public override void Help(Player p)
        {
            Player.SendMessage(p, "/resetbot - reloads the IRCBots. FOR EMERGENCIES ONLY!");
        }
    }
}
