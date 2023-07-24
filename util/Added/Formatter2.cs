/*
    Copyright 2015 MCGalaxy
    
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
using System;
using System.Collections.Generic;
using System.Text;
using MCForge.Commands;

namespace MCForge
{
    public static class Formatter
    {

        static void FindAliases(List<Alias> aliases, Command cmd, StringBuilder dst)
        {
            foreach (Alias a in aliases)
            {
                if (!a.Target.CaselessEq(cmd.name)) continue;

                dst.Append('/').Append(a.Trigger);
                if (a.Format == null) { dst.Append(", "); continue; }

                string name = String.IsNullOrEmpty(cmd.shortcut) ? cmd.name : cmd.shortcut;
                if (name.Length > cmd.name.Length) name = cmd.name;
                string args = a.Format.Replace("{args}", "[args]");

                dst.Append(" for /").Append(name + " " + args);
                dst.Append(", ");
            }
        }

        public static bool ValidMapName(Player p, string name)
        {
            if (Levelinfo.ValidName(name)) return true;
            p.SendMessage("\"" + name + "\" is not a valid level name.");
            return false;
        }

        static char[] separators = { '/', '\\', ':' };
        static char[] invalid = { '<', '>', '|', '"', '*', '?' };
    }
}
