/*
Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
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
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace MCForge {
    /// <summary> Static class for assisting with making web requests. </summary>
    public static class HttpUtil {

        public static WebClient CreateWebClient() { return new CustomWebClient(); }
        
        public static HttpWebRequest CreateRequest(string uri) {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.UserAgent = Server.SoftwareNameVersioned;
            return req;
        }
        
        public static void SetRequestData(WebRequest request, byte[] data) {
            request.ContentLength = data.Length;
            using (Stream w = request.GetRequestStream()) {
                w.Write(data, 0, data.Length);
            }
        }
        
        public static string GetResponseText(WebResponse response) {
            using (StreamReader r = new StreamReader(response.GetResponseStream())) {
                return r.ReadToEnd().Trim();
            }
        }
        
        /// <summary> Attempts to read the WebResponse in the given exception into a string </summary>
        /// <remarks> Returns null if the exception did not contain a readable WebResponse </remarks>
        public static string GetErrorResponse(Exception ex) {
            try {
                WebException webEx = ex as WebException;
                if (webEx != null && webEx.Response != null)
                    return GetResponseText(webEx.Response);
            } catch {  }
            return null;
        }
        
        /// <summary> Disposes the WebResponse in the given exception to avoid resource leakage </summary>
        /// <remarks> Does nothing if there is no WebResponse </remarks>
        public static void DisposeErrorResponse(Exception ex) {
            try {
                WebException webEx = ex as WebException;
                if (webEx != null && webEx.Response != null) webEx.Response.Close();
            } catch { }
        }
        

        class CustomWebClient : WebClient {
            protected override WebRequest GetWebRequest(Uri address) {
                HttpWebRequest req = (HttpWebRequest)base.GetWebRequest(address);
                req.UserAgent = Server.SoftwareNameVersioned;
                return (WebRequest)req;
            }
        }
        
        
        // TLS 1.1/1.2 do not exist in .NET 4.0 and cause a compilation failure
        public const SslProtocols TLS_11  = (SslProtocols)768;
        public const SslProtocols TLS_12  = (SslProtocols)3072;
        public const SslProtocols TLS_ALL = SslProtocols.Tls | TLS_11 | TLS_12;
        
        public static SslStream WrapSSLStream(Stream source, string host) {
            SslStream wrapped  = new SslStream(source);
            wrapped.AuthenticateAsClient(host, null, TLS_ALL, false);
            return wrapped;
        }

        /// <summary> Prefixes a URL by http:// if needed, and converts dropbox webpages to direct links. </summary>
        public static void FilterURL(ref string url)
        {
            if (!url.CaselessStarts("http://") && !url.CaselessStarts("https://"))
                url = "http://" + url;

            // a lot of people try linking to the dropbox page instead of directly to file, so auto correct
            if (url.CaselessStarts("http://www.dropbox"))
            {
                url = "http://dl.dropbox" + url.Substring("http://www.dropbox".Length);
                url = url.Replace("?dl=0", "");
            }
            else if (url.CaselessStarts("https://www.dropbox"))
            {
                url = "https://dl.dropbox" + url.Substring("https://www.dropbox".Length);
                url = url.Replace("?dl=0", "");
            }

            url = url.Replace("dl.dropboxusercontent.com", "dl.dropbox.com");
        }     
        static string DescribeError(Exception ex) {
            try {
                WebException webEx = (WebException)ex;
                // prefer explicit http status error codes if possible
                try {
                    int status = (int)((HttpWebResponse)webEx.Response).StatusCode;
                    return "(" + status + " error) from ";
                } catch {
                    return "(" + webEx.Status + ") from ";
                }
            } catch {
                return null;
            }
        }
   
        
        static byte[] DownloadData(Player p, string url, Uri uri) {
            byte[] data = null;
            try {
                using (WebClient client = CreateWebClient()) {
                    p.SendMessage("Downloading file from: &f" + url);
                    data = client.DownloadData(uri);
                }
                p.SendMessage("Finished downloading.");
            } catch (Exception ex) {                
                string msg = DescribeError(ex);
                
                if (msg == null) {
                    // unexpected error, log full error details
                    msg = "from ";
                    Server.s.Log("Error downloading " + url);
                } else {
                    // known error, so just log a warning
                    string logMsg = msg + url + Environment.NewLine + ex.Message;
                    Server.s.Log( "Error downloading " + logMsg);
                }
                return null;
            }
            return data;
        }
    }
}