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

namespace MCForge.Commands
{
    public class CmdUpdate : Command
    {
        public override string name { get { return "update"; } }
        public override string shortcut { get { return  ""; } }
        public override string type { get { return "Moderation"; } }
        public override bool museumUsable { get { return true; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Nobody; } }
        public CmdUpdate() { }

        public override void Use(Player p, string message)
        {

                if (p == null || p.group.Permission > defaultRank) MCForgeUpdater.Program.Main(null);

        }
        public override void Help(Player p)
        {
            Player.SendMessage(p, "/update - Updates the server if it's out of date");
            Player.SendMessage(p, "/update force - Forces the server to update");
        }
    }
}
