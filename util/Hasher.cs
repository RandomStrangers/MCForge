/*
	Copyright © 2011-2014 MCForge-Redux
		
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MCForge.Util {
    internal sealed class PasswordHasher {

        const string FILE_LOCATION = "extra/passwords/{0}.dat";

        internal static byte[] Compute(string salt, string plainText) {
            if ( string.IsNullOrEmpty(salt) ) {
                throw new ArgumentNullException("salt", "fileName is null or empty");
            }

            if ( string.IsNullOrEmpty(plainText) ) {
                throw new ArgumentNullException("plainText", "plainText is null or empty");
            }

            salt = salt.Replace("<", "(");
            salt = salt.Replace(">", ")");
            plainText = plainText.Replace("<", "(");
            plainText = plainText.Replace(">", ")");

            MD5 hash = MD5.Create();

            byte[] textBuffer = Encoding.ASCII.GetBytes(plainText);
            byte[] saltBuffer = Encoding.ASCII.GetBytes(salt);

            byte[] hashedTextBuffer = hash.ComputeHash(textBuffer);
            byte[] hashedSaltBuffer = hash.ComputeHash(saltBuffer);
            return hash.ComputeHash(hashedSaltBuffer.Concat(hashedTextBuffer).ToArray());
        }
            internal static void StoreHash(string salt, string plainText) {

            byte[] doubleHashedSaltBuffer = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Compute(salt, plainText)));

            if ( !File.Exists(string.Format(FILE_LOCATION, salt)) )
                using ( var disp = File.Create(string.Format(FILE_LOCATION, salt)) ) 

            using ( var Writer = File.OpenWrite(string.Format(FILE_LOCATION, salt)) ) {
                Writer.Write(doubleHashedSaltBuffer, 0, doubleHashedSaltBuffer.Length);
            }

        }

        internal static bool MatchesPass(string salt, string plainText) {

            if ( !File.Exists(string.Format(FILE_LOCATION, salt)) )
                return false;

            string hashes = File.ReadAllText(string.Format(FILE_LOCATION, salt));

            if ( hashes.Equals(Encoding.UTF8.GetString(Compute(salt, plainText))) ) {
                return true;
            }


            return false;

        }
        internal static byte[] Compute2(string salt2)
        {
            if (string.IsNullOrEmpty(salt2))
            {
                throw new ArgumentNullException("salt2", "fileName is null or empty");
            }
            salt2 = salt2.Replace("<", "(");
            salt2 = salt2.Replace(">", ")");
            MD5 hash2 = MD5.Create();
            byte[] salt2Buffer = Encoding.ASCII.GetBytes(salt2);
            byte[] hashedSalt2Buffer = hash2.ComputeHash(salt2Buffer);
            return hash2.ComputeHash(hashedSalt2Buffer.ToArray());
        }
    
        internal static void StoreHash2(string salt2, string plainText)
        {

            byte[] doubleHashedSalt2Buffer = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Compute(salt2, plainText)));

            if (!File.Exists(string.Format(FILE_LOCATION, salt2)))
                using (var disp = File.Create(string.Format(FILE_LOCATION, salt2)))

                using (var Writer = File.OpenWrite(string.Format(FILE_LOCATION, salt2)))
                {
                    Writer.Write(doubleHashedSalt2Buffer, 0, doubleHashedSalt2Buffer.Length);
                }

        }
        internal static bool MatchesPass2(string salt2, string plainText)
        {

            if (!File.Exists(string.Format(FILE_LOCATION, salt2)))
                return false;

            string hashes2 = File.ReadAllText(string.Format(FILE_LOCATION, salt2));

            if (hashes2.Equals(Encoding.UTF8.GetString(Compute(salt2, plainText))))
            {
                return true;
            }


            return false;

        }
        internal static byte[] Compute3(string salt3)
        {
            if (string.IsNullOrEmpty(salt3))
            {
                throw new ArgumentNullException("salt3", "fileName is null or empty");
            }
            salt3 = salt3.Replace("<", "(");
            salt3 = salt3.Replace(">", ")");
            MD5 hash3 = MD5.Create();
            byte[] salt3Buffer = Encoding.ASCII.GetBytes(salt3);
            byte[] hashedSalt3Buffer = hash3.ComputeHash(salt3Buffer);
            return hash3.ComputeHash(hashedSalt3Buffer.ToArray());
        }
        internal static void StoreHash3(string salt3, string plainText)
        {

            byte[] doubleHashedSalt3Buffer = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Compute(salt3, plainText)));

            if (!File.Exists(string.Format(FILE_LOCATION, salt3)))
                using (var disp = File.Create(string.Format(FILE_LOCATION, salt3)))

                using (var Writer = File.OpenWrite(string.Format(FILE_LOCATION, salt3)))
                {
                    Writer.Write(doubleHashedSalt3Buffer, 0, doubleHashedSalt3Buffer.Length);
                }

        }
        internal static bool MatchesPass3(string salt3, string plainText)
        {

            if (!File.Exists(string.Format(FILE_LOCATION, salt3)))
                return false;

            string hashes3 = File.ReadAllText(string.Format(FILE_LOCATION, salt3));

            if (hashes3.Equals(Encoding.UTF8.GetString(Compute(salt3, plainText))))
            {
                return true;
            }


            return false;
        }
        internal static byte[] Compute4(string salt4)
        {
            if (string.IsNullOrEmpty(salt4))
            {
                throw new ArgumentNullException("salt4", "fileName is null or empty");
            }
            salt4 = salt4.Replace("<", "(");
            salt4 = salt4.Replace(">", ")");
            MD5 hash4 = MD5.Create();
            byte[] salt4Buffer = Encoding.ASCII.GetBytes(salt4);
            byte[] hashedSalt4Buffer = hash4.ComputeHash(salt4Buffer);
            return hash4.ComputeHash(hashedSalt4Buffer.ToArray());
        }
        internal static void StoreHash4(string salt4, string plainText)
        {

            byte[] doubleHashedSalt4Buffer = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Compute(salt4, plainText)));

            if (!File.Exists(string.Format(FILE_LOCATION, salt4)))
                using (var disp = File.Create(string.Format(FILE_LOCATION, salt4)))

                using (var Writer = File.OpenWrite(string.Format(FILE_LOCATION, salt4)))
                {
                    Writer.Write(doubleHashedSalt4Buffer, 0, doubleHashedSalt4Buffer.Length);
                }

        }
        internal static bool MatchesPass4(string salt4, string plainText)
        {

            if (!File.Exists(string.Format(FILE_LOCATION, salt4)))
                return false;

            string hashes4 = File.ReadAllText(string.Format(FILE_LOCATION, salt4));

            if (hashes4.Equals(Encoding.UTF8.GetString(Compute(salt4, plainText))))
            {
                return true;
            }


            return false;
        }
    }
    }
