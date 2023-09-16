using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;

namespace OculusPrimeSwitcher
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                string? oculusPath = GetOculusPath();
                var result = GetSteamPaths();
                if (result == null || string.IsNullOrEmpty(oculusPath))
                {
                    return;
                }
                string startupPath = result["startupPath"];
                string vrServerPath = result["vrServerPath"];

                Process.Start(startupPath).WaitForExit();

                Stopwatch sw = Stopwatch.StartNew();
                while (true)
                {
                    if (sw.ElapsedMilliseconds >= 10000)
                    {
                        MessageBox.Show("SteamVR vrserver not found... (Did SteamVR crash?)");
                        return;
                    }

                    Process? vrServerProcess = Array.Find(Process.GetProcessesByName("vrserver"), process => process.MainModule.FileName == vrServerPath);
                    if (vrServerProcess == null)
                        continue;
                    vrServerProcess.WaitForExit();

                    Process? ovrServerProcess = Array.Find(Process.GetProcessesByName("OVRServer_x64"), process => process.MainModule.FileName == oculusPath);
                    if (ovrServerProcess == null)
                    {
                        MessageBox.Show("Oculus runtime not found...");
                        return;
                    }

                    ovrServerProcess.Kill();
                    ovrServerProcess.WaitForExit();
                    break;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"An exception occurred while attempting to find/start SteamVR...\n\nMessage: {e}");
            }
        }

        static string? GetOculusPath()
        {
            string oculusPath = Environment.GetEnvironmentVariable("OculusBase") ?? string.Empty;
            if (string.IsNullOrEmpty(oculusPath))
            {
                MessageBox.Show("Oculus installation environment not found...");
                return null;
            }

            oculusPath = Path.Combine(oculusPath, @"Support\oculus-runtime\OVRServer_x64.exe");
            if (!File.Exists(oculusPath))
            {
                MessageBox.Show("Oculus server executable not found...");
                return null;
            }

            return oculusPath;
        }

        public static Dictionary<string, string>? GetSteamPaths()
        {
            string openVrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"openvr\openvrpaths.vrpath");
            if (!File.Exists(openVrPath))
            {
                MessageBox.Show("OpenVR Paths file not found... (Has SteamVR been run once?)");
                return null;
            }

            try
            {
                var openvrPaths = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, dynamic>>(File.ReadAllText(openVrPath));
                string? location = openvrPaths?["runtime"]?[0]?.ToString();
                if (location == null)
                {
                    MessageBox.Show("Location not found in OpenVR Paths file.");
                    return null;
                }

                string startupPath = Path.Combine(location, @"bin\win64\vrstartup.exe");
                string serverPath = Path.Combine(location, @"bin\win64\vrserver.exe");

                if (!File.Exists(startupPath) || !File.Exists(serverPath))
                {
                    MessageBox.Show("SteamVR executable(s) do not exist... (Has SteamVR been run once?)");
                    return null;
                }

                return new Dictionary<string, string>
                {
                    { "startupPath", startupPath },
                    { "vrServerPath", serverPath }
                };
            }
            catch (Exception e)
            {
                MessageBox.Show($"Corrupt OpenVR Paths file found... (Has SteamVR been run once?)\n\nMessage: {e}");
                return null;
            }
        }
    }
}
