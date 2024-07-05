using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;

namespace MCForgeUpdater
{
    public static class HttpUtil
    {
        public static WebClient CreateWebClient() { return new CustomWebClient(); }
        public static HttpWebRequest CreateRequest(string uri)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.ServicePoint.BindIPEndPointDelegate = BindIPEndPointCallback;
            req.UserAgent = "MCForgeUpdater";
            return req;
        }
        public static void SetRequestData(WebRequest request, byte[] data)
        {
            request.ContentLength = data.Length;
            using (Stream w = request.GetRequestStream())
            {
                w.Write(data, 0, data.Length);
            }
        }
        public static INetListen Listener = new TcpListen();
        public static string GetResponseText(WebResponse response)
        {
            using (StreamReader r = new StreamReader(response.GetResponseStream()))
            {
                return r.ReadToEnd().Trim();
            }
        }
        public static string GetErrorResponse(Exception ex)
        {
            try
            {
                if (ex is WebException webEx && webEx.Response != null)
                    return GetResponseText(webEx.Response);
            }
            catch { }
            return null;
        }
        public static void DisposeErrorResponse(Exception ex)
        {
            try
            {
                if (ex is WebException webEx && webEx.Response != null) webEx.Response.Close();
            }
            catch { }
        }
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
        public const SslProtocols TLS_11 = (SslProtocols)768;
        public const SslProtocols TLS_12 = (SslProtocols)3072;
        public const SslProtocols TLS_ALL = SslProtocols.Tls | TLS_11 | TLS_12;
        public static SslStream WrapSSLStream(Stream source, string host)
        {
            SslStream wrapped = new SslStream(source);
            wrapped.AuthenticateAsClient(host, null, TLS_ALL, false);
            return wrapped;
        }
        static bool CheckHttpOrHttps(string url)
        {
            if (!url.Contains("://")) return true;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return true;
            string scheme = uri.Scheme;
            if (scheme.CaselessEq("http") || scheme.CaselessEq("https")) return true;
            Console.WriteLine("&WOnly http:// or https:// urls are supported, " +
                      "{0} is a {1}:// url", url, scheme);
            return false;
        }
        public static Uri GetUrl(ref string url)
        {
            if (!CheckHttpOrHttps(url)) return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                Console.WriteLine("&W{0} is not a valid URL.", url); return null;
            }
            return uri;
        }
        static string DescribeError(Exception ex)
        {
            try
            {
                WebException webEx = (WebException)ex;
                try
                {
                    int status = (int)((HttpWebResponse)webEx.Response).StatusCode;
                    return "(" + status + " error) from ";
                }
                catch
                {
                    return "(" + webEx.Status + ") from ";
                }
            }
            catch
            {
                return null;
            }
        }
        public static byte[] DownloadData(string url)
        {
            Uri uri = GetUrl(ref url);
            if (uri == null) return null;
            return DownloadData(url, uri);
        }
        static byte[] DownloadData(string url, Uri uri)
        {
            byte[] data = null;
            try
            {
                using (WebClient client = CreateWebClient())
                {
                    Console.WriteLine("Downloading file from: &f" + url);
                    data = client.DownloadData(uri);
                }
                Console.WriteLine("Finished downloading.");
            }
            catch (Exception ex)
            {
                string msg = DescribeError(ex);

                if (msg == null)
                {
                    msg = "from ";
                    Console.WriteLine("Error downloading " + url, ex);
                }
                else
                {
                    string logMsg = msg + url + Environment.NewLine + ex.Message;
                    Console.WriteLine("Error downloading " + logMsg);
                }
                Console.WriteLine("&WFailed to download {0}&f{1}", msg, url);
                return null;
            }
            return data;
        }
        public static byte[] DownloadImage(string url)
        {
            Uri uri = GetUrl(ref url);
            if (uri == null) return null;
            byte[] data = DownloadData(url, uri);
            if (data == null) Console.WriteLine("&WThe url may need to end with its extension (such as .jpg).");
            return data;
        }
    }
}