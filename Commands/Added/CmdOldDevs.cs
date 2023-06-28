/*
	Copyright 2010 MCLawl Team - Written by Valek (Modified for use with MCForge)
 
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
namespace MCForge.Commands {
	public class CmdOldDevs : Command {
		public override string name { get { return "olddevs"; } }
		public override string shortcut { get { return  "odev"; } }
		public override string type { get { return "information"; } }
		public override bool museumUsable { get { return true; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Banned; } }
		public CmdOldDevs() { }

		public override void Use(Player p, string message) {
			if ( message != "" ) { Help(p); return; }
			string olddevlist = "";
			string temp;
			foreach ( string dev in Server.oldDevs ) {
				temp = dev.Substring(0, 1);
				temp = temp.ToUpper() + dev.Remove(0, 1);
				olddevlist += temp + ", ";
			}
			olddevlist = olddevlist.Remove(olddevlist.Length - 2);
			Player.SendMessage(p, "&9Original MCForge Development Team: " + Server.DefaultColor + olddevlist + "&e.");
		}

		public override void Help(Player p) {
			Player.SendMessage(p, "/olddevs - Displays the list of original MCForge developers.");
		}
	}
}