using OpenFK.OFK.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml.Linq;

namespace OpenFK.OFK.Net
{
    static class UpdateManager
    {
        // ===================================
        // Update Manager
        // Handles OpenFK and FSGUI updates.
        // ===================================
        static XDocument UpdateStore;
        static XDocument FSUpdateStore;

        public static string RemoveBuildNumberFromVersion(string version)
        {
            return version.Substring(0, version.LastIndexOf("."));
        }

        /// <summary>
        /// Checks a remote XML store to find an update for OpenFK and FunkeySelectorGUI.
        /// </summary>
        public static void CheckUpdate()
        {
            //OpenFK version
            string currentOFKVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            currentOFKVersion = RemoveBuildNumberFromVersion(currentOFKVersion);
            string localVersion = $"OpenFK v{currentOFKVersion}";

            //FSGUI version - Checks the new and old name, favors the new one.
            bool isFSGUIHere = false;
            string fslocalVerNum = "";
            string fslocalVersion = "";
            
            string[] fsNames = { "FunkeySelectorGUI.exe", "FunkeySelector.exe" };
            foreach (string file in fsNames)
            {
                if (!File.Exists(file)) continue;

                fslocalVerNum = FileVersionInfo.GetVersionInfo(Path.GetFullPath(file)).FileVersion;
                fslocalVersion = $"FunkeySelectorGUI v{fslocalVerNum}";
                break;
            }

            LogManager.LogNetwork("[Update] Update Requested", "NetCommand");
            Globals.GameForm.SetVar(@"<progress percent=""0.25"" />");

            Globals.GameForm.SetVar(@"<progress percent=""25.00"" />");
            try
            {
                LogManager.LogNetwork("[Update] Downloading Update.xml from Codeberg", "NetCommand");
                UpdateStore = XDocument.Parse(HttpManager.HTTPGet(@"https://codeberg.org/OpenFunk/OpenFunk/raw/branch/main/update.xml"));
                LogManager.LogNetwork("[Update] Update.xml was downloaded", "NetCommand");
                string netVersion = UpdateStore.Root.Attribute("name").Value;
                string netVersionNum = UpdateStore.Root.Attribute("version").Value;
                string netVersionSize = UpdateStore.Root.Attribute("size").Value;
                Globals.GameForm.SetVar(@"<progress percent=""50.00"" />");
                if (currentOFKVersion != netVersionNum)
                {
                    LogManager.LogNetwork("[Update] An update is needed", "NetCommand");
                    UpdateStore.Save(Directory.GetCurrentDirectory() + @"\update.xml");
                    Globals.GameForm.SetVar(@"<checkupdate result=""2"" reason=""New version of OpenFK found."" version=""2009_07_16_544"" size=""" + netVersionSize + @""" curversion=""" + currentOFKVersion + @""" extversion=""" + netVersionNum + @""" extname=""" + netVersion + @""" />");
                    return;
                }
            }
            catch
            {
                LogManager.LogNetwork("[Update] [Error] Failed to check OpenFK update.", "NetCommand");
                Globals.GameForm.SetVar(@"<checkupdate result=""1"" reason=""Could not find the OpenFK update!"" />");
            }

            if (!isFSGUIHere)
            {
                Globals.GameForm.SetVar(@"<checkupdate result=""0"" reason=""Everything is up to date."" />");
                return;
            }

            Globals.GameForm.SetVar(@"<progress percent=""75.00"" />");
            try
            {
                LogManager.LogNetwork("[Update] Downloading FSGUI Update.xml from Codeberg", "NetCommand");
                FSUpdateStore = XDocument.Parse(HttpManager.HTTPGet(@"https://codeberg.org/OpenFunk/FunkeySelectorGUI/raw/branch/main/update.xml"));
                LogManager.LogNetwork("[Update] FSGUI Update.xml was downloaded", "NetCommand");
                string fsnetVersion = FSUpdateStore.Root.Attribute("name").Value;
                string fsnetVersionNum = FSUpdateStore.Root.Attribute("version").Value;
                string fsnetVersionSize = FSUpdateStore.Root.Attribute("size").Value;
                Globals.GameForm.SetVar(@"<progress percent=""90.00"" />");
                if (fslocalVerNum == fsnetVersionNum)
                {
                    Globals.GameForm.SetVar(@"<checkupdate result=""0"" reason=""Everything is up to date."" />");
                    return;
                }

                try
                {
                    Process process = Process.GetProcessesByName("FunkeySelectorGUI")[0];
                    process.Kill();
                }
                catch
                {
                    LogManager.LogNetwork("[Update] Cannot close FSGUI", "NetCommand");
                }
                LogManager.LogNetwork("[Update] A FSGUI update is needed", "NetCommand");
                Globals.GameForm.SetVar(@"<checkupdate result=""2"" reason=""New version of FSGUI found."" version=""2009_07_16_544"" size=""" + fsnetVersionSize + @""" curversion=""" + fslocalVerNum + @""" extversion=""" + fsnetVersionNum + @""" extname=""" + fsnetVersion + @""" />");
            }
            catch
            {
                LogManager.LogNetwork("[Update] [Error] Failed to check FSGUI update.", "NetCommand");
                Globals.GameForm.SetVar(@"<checkupdate result=""1"" reason=""Could not find the FunkeySelectorGUI update!"" />");
            }
        }

        /// <summary>
        /// Downloads the newly found update.
        /// </summary>
        public static void LoadUpdate()
        {
            try
            {
                if (FSUpdateStore != null)
                {
                    string fsnetDL = FSUpdateStore.Root.Attribute("url").Value;
                    using (var client = new WebClient())
                    {
                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        client.DownloadFile(fsnetDL, Directory.GetCurrentDirectory() + @"\FunkeySelectorGUI.exe");
                    }
                    Globals.GameForm.SetVar(@"<loadupdate result=""0"" reason=""good"" />");
                    LogManager.LogNetwork("[Update] Updated FSGUI successfuly.", "NetCommand");
                }
                else
                {
                    string netDL = UpdateStore.Root.Attribute(Environment.Is64BitProcess ? "url64" : "url32").Value;
                    
                    using (var client = new WebClient())
                    {
                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        client.DownloadFile(netDL, Directory.GetCurrentDirectory() + @"\tmpdl.zip");
                    }
                    UpdateStore.Save(Directory.GetCurrentDirectory() + @"\update.xml");
                    Directory.CreateDirectory(Path.GetDirectoryName(Directory.GetCurrentDirectory() + @"\tmpdl\"));
                    System.IO.Compression.ZipFile.ExtractToDirectory(Directory.GetCurrentDirectory() + @"\tmpdl.zip", Directory.GetCurrentDirectory() + @"\tmpdl\");
                    Globals.GameForm.SetVar(@"<loadupdate result=""0"" reason=""good"" />");
                    LogManager.LogNetwork("[Update] OpenFK update loaded successfuly.", "NetCommand");
                    Globals.WasUpdated = true;
                }
            }
            catch
            {
                LogManager.LogNetwork("[Update] [Error] Failed to download update.", "NetCommand");
                Globals.GameForm.SetVar(@"<loadupdate result=""1"" reason=""The update has failed! Try restarting OpenFK..."" />");
            }
        }

        /// <summary>
        /// Copies tmpdl during the /update stage of OpenFK.
        /// </summary>
        public static void InstallUpdate()
        {
            foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory()))
                File.Copy(file, Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).ToString(), Path.GetFileName(file)), true);
            ProcessStartInfo updateRestart = new ProcessStartInfo(Directory.GetParent(Directory.GetCurrentDirectory()) + @"\OpenFK.exe");
            updateRestart.WorkingDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).ToString();
            updateRestart.UseShellExecute = false;
            Process.Start(updateRestart);
        }
    }
}
