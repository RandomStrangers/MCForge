using MCForge_.Gui;
using System;
using System.Net;
using System.IO;
using System.Reflection;

namespace MCForge
{
    internal class Levelinfo
    {
        public static bool ValidName(string map)
        {
            foreach (char c in map)
            {
                if (!Database.ValidNameChar(c)) return false;
            }
            return true;
        }
        public static string[] AllMapFiles()
        {
            return Directory.GetFiles("levels", "*.mcf");
        }

        public static string[] AllMapNames()
        {
            string[] files = AllMapFiles();
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            }
            return files;
        }
    }
    public class utils3
    {
        static void EnableCLIMode()
        {
            try
            {
                Server.CLIMode = true;
            }
            catch
            {
                // in case user is running CLI with older MCForge dll which lacked CLIMode field
            }
            Server.RestartPath = Assembly.GetEntryAssembly().Location;
        }
    }
    public class Utils2
    {
        public static void CheckFile(string file)
        {
            if (File.Exists(file)) return;

            Server.s.Log(file + " doesn't exist, Downloading..");
            try
            {
                using (WebClient client = HttpUtil.CreateWebClient())
                {
                    client.DownloadFile(MCForgeUpdater.Updater.BaseURL + file, file);
                }
                if (File.Exists(file))
                {
                    Server.s.Log(file + " download succesful!");
                }
            }
            catch (Exception ex)
            {
                Server.s.Log("Downloading " + file + " failed, try again later");
            }
        }
    }
}
