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

                // Get the path to the Oculus executable
                string? oculusPath = GetOculusPath();
                if (string.IsNullOrEmpty(oculusPath))
                {
                    LogError("Oculus path not found. Exiting...");
                    return;
                }

                // Get the paths to the SteamVR executables
                var steamPaths = GetSteamPaths();
                if (steamPaths == null)
                {
                    LogError("Steam paths not found. Exiting...");
                    return;
                }

                string startupPath = steamPaths["startupPath"];
                string vrServerPath = steamPaths["vrServerPath"];

                // Start the Oculus executable
                Log($"Starting OculusDash from path: {oculusPath}");
                Process.Start(oculusPath);

                // Start the SteamVR executable
                Log($"Starting SteamVR from path: {startupPath}");
                Process.Start(startupPath);

                // Wait for the vrserver.exe process to start
                WaitForProcessToStart("vrserver");

                // Monitor the OVRService process
                MonitorOVRService();

                // Wait for the vrserver.exe process to exit
                WaitForProcessToExit("vrserver");

                // Kill the OVRServer_x64.exe process if it's still running
                KillProcess("OVRServer_x64", oculusPath);
            }
            catch (Exception e)
            {
                // Log any exceptions that occur
                LogError($"An exception occurred: {e}");
            }
        }

        /// <summary>
        /// Retrieves the path to the Oculus executable.
        /// </summary>
        static string? GetOculusPath()
        {
            // Get the Oculus installation path from the environment variables
            string? oculusPath = Environment.GetEnvironmentVariable("OculusBase");
            if (string.IsNullOrEmpty(oculusPath))
            {
                LogError("Oculus installation environment not found.");
                return null;
            }

            // Construct the full path to the Oculus server executable
            oculusPath = Path.Combine(oculusPath, @"Support\oculus-runtime\OVRServer_x64.exe");
            if (!File.Exists(oculusPath))
            {
                LogError("Oculus server executable not found.");
                return null;
            }

            return oculusPath;
        }

        /// <summary>
        /// Retrieves the paths to the SteamVR executables.
        /// </summary>
        public static Dictionary<string, string>? GetSteamPaths()
        {
            // Construct the path to the OpenVR configuration file
            string openVrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"openvr\openvrpaths.vrpath");
            if (!File.Exists(openVrPath))
            {
                LogError("OpenVR Paths file not found. Has SteamVR been run once?");
                return null;
            }

            try
            {
                // Read the OpenVR configuration file and parse its JSON content
                string openvrJsonString = File.ReadAllText(openVrPath);
                var openvrPaths = JObject.Parse(openvrJsonString);

                // Extract the paths to the SteamVR executables
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

        /// <summary>
        /// Waits for a specific process to start.
        /// </summary>
        static void WaitForProcessToStart(string processName)
        {
            Log($"Waiting for {processName} to start...");
            while (Process.GetProcessesByName(processName).Length == 0)
            {
                System.Threading.Thread.Sleep(500);
            }
            Log($"{processName} started.");
        }

        /// <summary>
        /// Waits for a specific process to exit.
        /// </summary>
        static void WaitForProcessToExit(string processName)
        {
            Log($"Waiting for {processName} to exit...");
            while (Process.GetProcessesByName(processName).Length > 0)
            {
                System.Threading.Thread.Sleep(500);
            }
            Log($"{processName} exited.");
        }

        /// <summary>
        /// Kills a specific process.
        /// </summary>
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
        /// Then restarts the OVRService after a delay.
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

                    // Restart OVRService
                    Log("Restarting OVRService...");
                    Process.Start("OVRServiceLauncher.exe", "-start");
                    Log("OVRService restarted.");

                    break;
                }
                System.Threading.Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Kills all SteamVR related processes.
        /// </summary>
        static void KillSteamVRProcesses()
        {
            foreach (var processName in new[] { "vrserver", "vrmonitor", "vrcompositor" })
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    process.Kill();
                    process.WaitForExit();
                }
            }
        }

        /// <summary>
        /// Logs a message with a timestamp.
        /// </summary>
        static void Log(string message)
        {
            string logMessage = $"{DateTime.Now}: {message}";
            Console.WriteLine(logMessage);
            File.AppendAllText("OculusPrimeSwitcher.log", logMessage + Environment.NewLine);
        }

        /// <summary>
        /// Logs an error message with a timestamp.
        /// </summary>
        static void LogError(string message)
        {
            Log($"ERROR: {message}");
        }
    }
}
