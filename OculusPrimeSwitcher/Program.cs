using System;
using System.Diagnostics;
using System.IO;
using System.Management; // For monitoring processes
using Newtonsoft.Json.Linq;

namespace OculusPrimeSwitcher
{
    public class Program
    {
        private static ManagementEventWatcher? startWatcher;
        private static ManagementEventWatcher? stopWatcher;

        public static void Main()
        {
            // Temporary monitoring for debugging purposes
            StartMonitoringOculusProcesses();

            try
            {
                string? oculusPath = GetOculusPath();
                var steamPaths = GetSteamPaths();
                if (steamPaths == null || string.IsNullOrEmpty(oculusPath))
                {
                    Log("Failed to retrieve Oculus or Steam paths.");
                    return;
                }

                string startupPath = steamPaths.Item1;
                string vrServerPath = steamPaths.Item2;

                Process.Start(startupPath); // Start SteamVR without waiting for it to exit

                while (true)
                {
                    Process? vrServerProcess = GetProcessByNameAndPath("vrserver", vrServerPath);
                    if (vrServerProcess != null)
                    {
                        vrServerProcess.WaitForExit();
                        break;
                    }
                }

                Process? ovrServerProcess = GetProcessByNameAndPath("OVRServer_x64", oculusPath);
                if (ovrServerProcess != null)
                {
                    ovrServerProcess.Kill();
                    ovrServerProcess.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Log($"An exception occurred: {e.Message}");
            }
        }

        static string? GetOculusPath()
        {
            string? oculusPath = Environment.GetEnvironmentVariable("OculusBase");
            if (string.IsNullOrEmpty(oculusPath))
            {
                Log("Oculus installation environment not found.");
                return null;
            }

            oculusPath = Path.Combine(oculusPath, @"Support\oculus-runtime\OVRServer_x64.exe");
            if (!File.Exists(oculusPath))
            {
                Log("Oculus server executable not found.");
                return null;
            }

            return oculusPath;
        }

        public static Tuple<string, string>? GetSteamPaths()
        {
            string openVrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"openvr\openvrpaths.vrpath");
            if (!File.Exists(openVrPath))
            {
                Log("OpenVR Paths file not found.");
                return null;
            }

            try
            {
                var openvrPaths = JObject.Parse(File.ReadAllText(openVrPath));
                string location = openvrPaths["runtime"][0].ToString();
                string startupPath = Path.Combine(location, @"bin\win64\vrstartup.exe");
                string serverPath = Path.Combine(location, @"bin\win64\vrserver.exe");

                if (!File.Exists(startupPath) || !File.Exists(serverPath))
                {
                    Log("SteamVR executables not found.");
                    return null;
                }

                return new Tuple<string, string>(startupPath, serverPath);
            }
            catch (Exception e)
            {
                Log($"Error reading OpenVR Paths file: {e.Message}");
                return null;
            }
        }

        static Process? GetProcessByNameAndPath(string processName, string processPath)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                if (process.MainModule.FileName.Equals(processPath, StringComparison.OrdinalIgnoreCase))
                {
                    return process;
                }
            }
            return null;
        }

        static void Log(string message)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OculusPrimeSwitcher.log");
            File.AppendAllText(logPath, $"{DateTime.Now}: {message}\n");
        }

        // Temporary monitoring for debugging purposes
        static void StartMonitoringOculusProcesses()
        {
            startWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatcher.EventArrived += OnProcessStarted;
            startWatcher.Start();

            stopWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            stopWatcher.EventArrived += OnProcessStopped;
            stopWatcher.Start();
        }

        // Temporary monitoring for debugging purposes
        static void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();
            if (processName.ToLower().Contains("oculus"))
            {
                Log($"Oculus-related process started: {processName}");
            }
        }

        // Temporary monitoring for debugging purposes
        static void OnProcessStopped(object sender, EventArrivedEventArgs e)
        {
            string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();
            if (processName.ToLower().Contains("oculus"))
            {
                Log($"Oculus-related process stopped: {processName}");
            }
        }
    }
}
