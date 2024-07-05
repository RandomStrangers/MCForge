using System.IO;
namespace MCForgeUpdater
{
    public static class AtomicIO
    {
        public static bool TryDelete(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }
        public static bool TryMove(string curPath, string newPath)
        {
            try
            {
                File.Move(curPath, newPath);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }
        public static string[] TryGetFiles(string directory, string searchPattern)
        {
            try
            {
                return Directory.GetFiles(directory, searchPattern);
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }
    }
}