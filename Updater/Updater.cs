using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
namespace MCForgeUpdater
{
    public static class Updater
    {
        class CustomWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                HttpWebRequest req = (HttpWebRequest)base.GetWebRequest(address);
                req.ServicePoint.BindIPEndPointDelegate = BindIPEndPointCallback;
                req.UserAgent = "MCForgeUpdater";
                return req;
            }
        }
        public static INetListen Listener = new TcpListen();
        static IPEndPoint BindIPEndPointCallback(ServicePoint servicePoint, IPEndPoint remoteEP, int retryCount)
        {
            IPAddress localIP;
            if (Listener.IP != null)
            {
                localIP = Listener.IP;
            }
            else if (!IPAddress.TryParse("0.0.0.0", out localIP))
            {
                return null;
            }
            if (remoteEP.AddressFamily != localIP.AddressFamily) return null;
            return new IPEndPoint(localIP, 0);
        }

        public static WebClient CreateWebClient() { return new CustomWebClient(); }
        public const string BaseURL = "https://github.com/RandomStrangers/MCForge/raw/master/Uploads/";
        public static string dll = BaseURL + "MCForge_.dll";
        public static string cli = BaseURL + "MCForgeCLI.exe";
        public static string exe = BaseURL + "MCForge.exe";

        public static void PerformUpdate()
        {
            try
            {
                try
                {
                    DeleteFiles("MCForge.update", "MCForge_.update", "MCForgeCLI.update",
                        "prev_MCForge.exe", "prev_MCForge_.dll", "prev_MCForgeCLI.exe");
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error deleting files:");
                    Console.WriteLine(e.ToString());
                    Console.ReadKey(false);
                    return;
                }
                    try
                    {
                        WebClient client = HttpUtil.CreateWebClient();
                        File.Move("MCForge.exe", "prev_MCForge.exe");
                        File.Move("MCForgeCLI.exe", "prev_MCForgeCLI.exe");
                        File.Move("MCForge_.dll", "prev_MCForge_.dll");
                        client.DownloadFile(dll, "MCForge_.update");
                        client.DownloadFile(cli, "MCForgeCLI.update");
                        client.DownloadFile(exe, "MCForge.update");

                }
                catch (Exception x) 
                    {
                        Console.WriteLine("Error downloading update:");
                        Console.WriteLine(x.ToString());
                        Console.ReadKey(false);
                        return;
                    }
                File.Move("MCForge.update", "MCForge.exe");
                File.Move("MCForgeCLI.update", "MCForgeCLI.exe");
                File.Move("MCForge_.update", "MCForge_.dll");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error performing update:");
                Console.WriteLine(ex.ToString());
                Console.ReadKey(false);
                return;
            }
        }
        static void DeleteFiles(params string[] paths)
        {
            foreach (string path in paths) { AtomicIO.TryDelete(path); }
        }
    }
}
