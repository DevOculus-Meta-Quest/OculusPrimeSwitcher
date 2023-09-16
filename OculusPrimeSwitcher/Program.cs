using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace OculusPrimeSwitcher
{
    public class Program
    {
        public static async Task Main()
        {
            try
            {
                string oculusPath = GetOculusPath();
                Dictionary<string, string> steamPaths = GetSteamPaths();

                if (steamPaths == null || string.IsNullOrEmpty(oculusPath))
                {
                    return;
                }

                await StartAndMonitorSteamVR(steamPaths["startupPath"], steamPaths["serverPath"], oculusPath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"An exception occurred: {e.Message}");
            }
        }

        static string GetOculusPath()
        {
            string oculusBasePath = Environment.GetEnvironmentVariable("OculusBase");
            if (string.IsNullOrEmpty(oculusBasePath))
            {
                MessageBox.Show("Oculus installation environment not found...");
                return null;
            }

            string oculusServerPath = Path.Combine(oculusBasePath, @"Support\oculus-runtime\OVRServer_x64.exe");
            if (!File.Exists(oculusServerPath))
            {
                MessageBox.Show("Oculus server executable not found...");
                return null;
            }

            return oculusServerPath;
        }

        public static Dictionary<string, string> GetSteamPaths()
        {
            string openVrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"openvr\openvrpaths.vrpath");
            if (!File.Exists(openVrPath))
            {
                MessageBox.Show("OpenVR Paths file not found. Has SteamVR been run once?");
                return null;
            }

            try
            {
                var openvrPaths = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(openVrPath));
                string runtimeLocation = openvrPaths["runtime"][0].ToString();
                var paths = new Dictionary<string, string>
                {
                    ["startupPath"] = Path.Combine(runtimeLocation, @"bin\win64\vrstartup.exe"),
                    ["serverPath"] = Path.Combine(runtimeLocation, @"bin\win64\vrserver.exe")
                };

                if (!File.Exists(paths["startupPath"]) || !File.Exists(paths["serverPath"]))
                {
                    MessageBox.Show("SteamVR executables not found. Has SteamVR been run once?");
                    return null;
                }

                return paths;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Corrupt OpenVR Paths file found. Has SteamVR been run once? Error: {e.Message}");
                return null;
            }
        }

        public static async Task StartAndMonitorSteamVR(string startupPath, string vrServerPath, string oculusPath)
        {
            Process.Start(startupPath).WaitForExit();

            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                if (sw.ElapsedMilliseconds >= 10000)
                {
                    MessageBox.Show("SteamVR vrserver not found. Did SteamVR crash?");
                    return;
                }

                Process vrServerProcess = Process.GetProcessesByName("vrserver").FirstOrDefault(process => process.MainModule.FileName == vrServerPath);
                if (vrServerProcess == null)
                    continue;

                await vrServerProcess.WaitForExitAsync();

                Process ovrServerProcess = Process.GetProcessesByName("OVRServer_x64").FirstOrDefault(process => process.MainModule.FileName == oculusPath);
                if (ovrServerProcess == null)
                {
                    MessageBox.Show("Oculus runtime not found.");
                    return;
                }

                await Task.Delay(5000); // Wait for 5 seconds before killing the Oculus process.

                ovrServerProcess.Kill();
                await ovrServerProcess.WaitForExitAsync();
                break;
            }
        }
    }
}
