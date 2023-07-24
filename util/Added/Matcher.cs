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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace MCForge
{

    /// <summary> Finds partial matches of a 'name' against the names of the items in an enumerable. </summary>
    /// <remarks> returns number of matches found, and the matching item if only 1 match is found. </remarks>
    public static class Matcher
    {
        public static string FindMaps(Player pl, string name)
        {
            if (!Formatter.ValidMapName(pl, name)) return null;
            int matches;
            return Find(pl, name, out matches, Levelinfo.AllMapNames(),
                        null, l => l, "levels", 10);
        }
        /// <summary> Finds partial matches of 'color' against the list of colors. </summary>
        public static string FindColor(Player p, string color)
        {
            int matches;
            ColorDesc desc = Find(p, color, out matches, Colors.List,
                                  col => !col.Undefined, col => col.Name, "colors", 20);
            return desc.Undefined ? null : "&" + desc.Code;
        }
        /// <summary> Finds partial matches of 'name' against the names of the items in the 'items' enumerable. </summary>
        /// <returns> If exactly one match, the matching item. </returns>
        public static T Find<T>(Player p, string name, out int matches, IEnumerable<T> items,
                                Predicate<T> filter, StringFormatter<T> nameGetter, string group, int limit = 5)
        {
            return Find<T>(p, name, out matches, items, filter, nameGetter, nameGetter, group, limit);
        }


        /// <summary> Finds partial matches of 'name' against the names of the items in the 'items' enumerable. </summary>
        /// <returns> If exactly one match, the matching item. </returns>
        public static T Find<T>(Player p, string name, out int matches, IEnumerable<T> items,
                                Predicate<T> filter, StringFormatter<T> nameGetter,
                                StringFormatter<T> itemFormatter, string group, int limit = 5)
        {
            T match = default(T); matches = 0;
            StringBuilder output = new StringBuilder();
            const StringComparison comp = StringComparison.OrdinalIgnoreCase;

            foreach (T item in items)
            {
                if (filter != null && !filter(item)) continue;
                string itemName = nameGetter(item);
                if (itemName.Equals(name, comp)) { matches = 1; return item; }
                if (itemName.IndexOf(name, comp) < 0) continue;

                match = item; matches++;
                if (matches <= limit)
                {
                    output.Append(itemFormatter(item)).Append("&S, ");
                }
                else if (matches == limit + 1)
                {
                    output.Append("(and more), ");
                }
            }

            if (matches == 1) return match;
            if (matches == 0)
            {
                p.SendMessage("No " + group + " match " + name); return default(T);
            }

            string count = matches > limit ? limit + "+ " : matches + " ";
            string names = output.ToString(0, output.Length - 2);

            Player.SendMessage(p, count + group + name);
            p.SendMessage(names);
            return default(T);
        }


        /// <summary> Filters the given list of items to matching item names. Accepts * and ? wildcard tokens. </summary>
        public static List<string> Filter<T>(IList<T> input, string keyword, StringFormatter<T> nameGetter,
                                          Predicate<T> filter = null, StringFormatter<T> listFormatter = null)
        {
            List<string> matches = new List<string>();
            Regex regex = null;
            // wildcard matching
            if (keyword.Contains("*") || keyword.Contains("?"))
            {
                string pattern = "^" + Regex.Escape(keyword).Replace("\\?", ".").Replace("\\*", ".*") + "$";
                regex = new Regex(pattern, RegexOptions.IgnoreCase);
            }

            foreach (T item in input)
            {
                if (filter != null && !filter(item)) continue;
                string name = nameGetter(item);

                if (regex != null) { if (!regex.IsMatch(name)) continue; }
                else { if (!name.CaselessContains(keyword)) continue; }

                // format this item for display
                if (listFormatter != null) name = listFormatter(item);
                matches.Add(name);
            }
            return matches;
        }
    }
}
