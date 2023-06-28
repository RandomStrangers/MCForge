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
using System;
namespace MCForge.Commands
{
    public class CmdCCHeartbeat2 : Command
    {
        public override string name { get { return "ccheartbeat2"; } }
        public override string shortcut { get { return  "ccbeat2"; } }
        public override string type { get { return "other"; } }
        public override bool museumUsable { get { return true; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Nobody; } }
        public CmdCCHeartbeat2() { }

        public override void Use(Player p, string message)
        {
            try
            {
                Heart.Pump(new ClassiCubeBeat2());
            }
            catch (Exception e)
            {
                Server.s.Log("Error with ClassiCube2 pump.");
                Server.ErrorLog(e);
            }
            Player.SendMessage(p, "Heartbeat pump sent.");
            Player.SendMessage(p, "ClassiCube2 URL found: " + Server.CCURL2);
        }

        public override void Help(Player p)
        {
            Player.SendMessage(p, "/ccheartbeat2 - Forces a pump for the ClassiCube2 heartbeat.  DEBUG PURPOSES ONLY.");
        }
    }
}
