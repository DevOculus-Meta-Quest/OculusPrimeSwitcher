using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OculusPrimeSwitcher
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                // Log the start of the application
                Log("Application started.");

                string? oculusPath = GetOculusPath();
                if (string.IsNullOrEmpty(oculusPath))
                {
                    LogError("Oculus path not found. Exiting...");
                    return;
                }

                var steamPaths = GetSteamPaths();
                if (steamPaths == null)
                {
                    LogError("Steam paths not found. Exiting...");
                    return;
                }

                string startupPath = steamPaths["startupPath"];
                string vrServerPath = steamPaths["vrServerPath"];

                Log($"Starting OculusDash from path: {oculusPath}");
                Process.Start(oculusPath);

                Log($"Starting SteamVR from path: {startupPath}");
                Process.Start(startupPath);

                // Wait for vrserver.exe to start
                WaitForProcessToStart("vrserver");

                // Wait for vrserver.exe to exit
                WaitForProcessToExit("vrserver");

                // Kill OVRServer_x64.exe if it's still running
                KillProcess("OVRServer_x64", oculusPath);

                // Monitor the OVRService process
                MonitorOVRService();
            }
            catch (Exception e)
            {
                // Log any exceptions that occur
                LogError($"An exception occurred: {e}");
            }
        }

        static string? GetOculusPath()
        {
            string? oculusPath = Environment.GetEnvironmentVariable("OculusBase");
            if (string.IsNullOrEmpty(oculusPath))
            {
                LogError("Oculus installation environment not found.");
                return null;
            }

            oculusPath = Path.Combine(oculusPath, @"Support\oculus-runtime\OVRServer_x64.exe");
            if (!File.Exists(oculusPath))
            {
                LogError("Oculus server executable not found.");
                return null;
            }

            return oculusPath;
        }

        public static Dictionary<string, string>? GetSteamPaths()
        {
            string openVrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"openvr\openvrpaths.vrpath");
            if (!File.Exists(openVrPath))
            {
                LogError("OpenVR Paths file not found. Has SteamVR been run once?");
                return null;
            }

            try
            {
                string openvrJsonString = File.ReadAllText(openVrPath);
                var openvrPaths = JObject.Parse(openvrJsonString);

                string location = openvrPaths["runtime"][0].ToString();
                string startupPath = Path.Combine(location, @"bin\win64\vrstartup.exe");
                string serverPath = Path.Combine(location, @"bin\win64\vrserver.exe");

                if (!File.Exists(startupPath) || !File.Exists(serverPath))
                {
                    LogError("SteamVR executables do not exist. Has SteamVR been run once?");
                    return null;
                }

                return new Dictionary<string, string>
                {
                    {"startupPath", startupPath},
                    {"vrServerPath", serverPath}
                };
            }
            catch (Exception e)
            {
                LogError($"Error reading OpenVR Paths file: {e}");
                return null;
            }
        }

        static void WaitForProcessToStart(string processName)
        {
            Log($"Waiting for {processName} to start...");
            while (Process.GetProcessesByName(processName).Length == 0)
            {
                System.Threading.Thread.Sleep(500);
            }
            Log($"{processName} started.");
        }

        static void WaitForProcessToExit(string processName)
        {
            Log($"Waiting for {processName} to exit...");
            while (Process.GetProcessesByName(processName).Length > 0)
            {
                System.Threading.Thread.Sleep(500);
            }
            Log($"{processName} exited.");
        }

        static void KillProcess(string processName, string path)
        {
            var process = Array.Find(Process.GetProcessesByName(processName), p => p.MainModule.FileName == path);
            if (process != null)
            {
                Log($"Killing process: {processName}");
                process.Kill();
                process.WaitForExit();
                Log($"{processName} killed.");
            }
        }

        /// <summary>
        /// Monitors the OVRService process and kills SteamVR processes when OVRService stops.
        /// Then restarts the OVRService after a delay if it's not already running.
        /// </summary>
        static void MonitorOVRService()
        {
            Log("Monitoring OVRService...");
            while (true)
            {
                var ovrService = Process.GetProcessesByName("OVRService");
                if (ovrService.Length == 0)
                {
                    Log("OVRService stopped. Killing SteamVR processes...");
                    KillSteamVRProcesses();

                    // Wait for 1 second
                    System.Threading.Thread.Sleep(1000);

                    // Check if OVRService is not running before trying to restart it
                    if (Process.GetProcessesByName("OVRService").Length == 0)
                    {
                        // Restart OVRService
                        Log("Restarting OVRService...");
                        Process.Start("OVRServiceLauncher.exe", "-start");
                        Log("OVRService restarted.");
                    }

                    break;
                }
                System.Threading.Thread.Sleep(500);
            }
        }

        static void KillSteamVRProcesses()
        {
            // Kill vrserver.exe if it's still running
            KillProcess("vrserver", string.Empty);

            // Kill vrstartup.exe if it's still running
            KillProcess("vrstartup", string.Empty);
        }

        static void Log(string message)
        {
            string logMessage = $"{DateTime.Now}: {message}";
            Console.WriteLine(logMessage);
            File.AppendAllText("OculusPrimeSwitcher.log", logMessage + Environment.NewLine);
        }

        static void LogError(string message)
        {
            Log($"ERROR: {message}");
        }
    }
}
