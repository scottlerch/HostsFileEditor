using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace HostsFileEditor
{
    public class GetHostsFileFromServer
    {

        public class TrustAllCertificatePolicy : System.Net.ICertificatePolicy
        {
            public TrustAllCertificatePolicy() { }
            public bool CheckValidationResult(ServicePoint sp,
                X509Certificate cert,
                WebRequest req,
                int problem)
            {
                return true;
            }
        }

        

        public static Dictionary<string, bool> DownloadFiles(string url, string username, string password)
        {
            List<string> filesToDownload = GetListOfFilesFromServer(url, username, password);
            Dictionary<string, bool> downloadResults = new Dictionary<string, bool>();
            foreach (string fileName in filesToDownload)
            {
                downloadResults.Add(fileName, readNewHostsFile(url, username, password, fileName));
            }

            return downloadResults;
        }

        private static bool readNewHostsFile(string url, string username, string password, string filename)
        {
            StringBuilder HostsFile = new StringBuilder();

            try
            {
                HttpWebRequest reqhttp = (HttpWebRequest)HttpWebRequest.Create(new Uri(url + filename));
                reqhttp.Credentials = new NetworkCredential(username, password);
                reqhttp.Proxy = new WebProxy();
                ServicePointManager.CertificatePolicy = new TrustAllCertificatePolicy();
                HttpWebResponse response = (HttpWebResponse)reqhttp.GetResponse();
                Stream httpstream = response.GetResponseStream();

                StreamReader sr = new StreamReader(httpstream, true);
                if (sr != null)
                {
                    while (sr.EndOfStream != true)
                    {
                        
                        //HostsModList.ModDirectory
                        HostsFile.AppendLine(sr.ReadLine());
                    }
                    //used just for testing.
                    //System.Windows.Forms.MessageBox.Show(hosts.Count.ToString() + " hosts to update.", title);
                    sr.Close();
                }
            }
            catch (WebException we)
            {
                System.Windows.Forms.MessageBox.Show(we.Message);
                return false;
            }
            //TODO:
            //Not sure this is really safe...what if someone else has it open?
            File.WriteAllText(Path.Combine(HostsModList.ModDirectory, filename), HostsFile.ToString());
            return true;
        }

        /// <summary>
        /// Gets all the hosts file names from the server.
        /// </summary>
        /// <param name="URL"></param>
        /// <returns></returns>
        private static List<string> GetListOfFilesFromServer(string URL, string username, string password)
        {
            List<string> hostsFileNames = new List<string>();
            string regexPattern = @"<a href=\"".*\"">(?<name>.*)</a>";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Credentials = new NetworkCredential(username, password);
            request.Proxy = new WebProxy();
            ServicePointManager.CertificatePolicy = new TrustAllCertificatePolicy();
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string html = reader.ReadToEnd();
                    Regex regex = new Regex(regexPattern,RegexOptions.IgnoreCase);
                    MatchCollection matches = regex.Matches(html);
                    if (matches.Count > 0)
                    {
                        foreach (Match match in matches)
                        {
                            if (match.Success)
                            {

                                hostsFileNames.Add(match.Groups["name"].ToString());
                            }
                        }
                    }
                }
            }
            return hostsFileNames;
        }
    }
}
