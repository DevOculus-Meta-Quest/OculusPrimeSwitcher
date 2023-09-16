using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;

namespace OculusPrimeSwitcher
{
    public class Program
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OculusPrimeSwitcher.log");

        public static void Main()
        {
            try
            {
                Log("Application started.");

                string? oculusPath = GetOculusPath();
                var result = GetSteamPaths();
                if (result == null || string.IsNullOrEmpty(oculusPath))
                {
                    return;
                }
                string startupPath = result["startupPath"];
                string vrServerPath = result["vrServerPath"];

                Log($"Starting SteamVR from path: {startupPath}");
                Process.Start(startupPath).WaitForExit();

                Stopwatch sw = Stopwatch.StartNew();
                while (true)
                {
                    if (sw.ElapsedMilliseconds >= 10000)
                    {
                        LogError("SteamVR vrserver not found. Did SteamVR crash?");
                        return;
                    }

                    Process? vrServerProcess = Array.Find(Process.GetProcessesByName("vrserver"), process => process.MainModule.FileName == vrServerPath);
                    if (vrServerProcess == null)
                        continue;
                    Log($"SteamVR vrserver process exited. Path: {vrServerProcess.MainModule.FileName}");
                    vrServerProcess.WaitForExit();

                    Process? ovrServerProcess = Array.Find(Process.GetProcessesByName("OVRServer_x64"), process => process.MainModule.FileName == oculusPath);
                    if (ovrServerProcess == null)
                    {
                        LogError("Oculus runtime not found.");
                        return;
                    }

                    Log($"Killing Oculus runtime process. Path: {ovrServerProcess.MainModule.FileName}");
                    ovrServerProcess.Kill();
                    ovrServerProcess.WaitForExit();
                    break;
                }
            }
            catch (Exception e)
            {
                LogError($"An exception occurred: {e}");
            }
        }

        static string? GetOculusPath()
        {
            string oculusPath = Environment.GetEnvironmentVariable("OculusBase") ?? string.Empty;
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

            Log($"Found Oculus path: {oculusPath}");
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
                var openvrPaths = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, dynamic>>(File.ReadAllText(openVrPath));
                string? location = openvrPaths?["runtime"]?[0]?.ToString();

                if (location == null)
                {
                    LogError("Location not found in OpenVR Paths file.");
                    return null;
                }

                string startupPath = Path.Combine(location, @"bin\win64\vrstartup.exe");
                string serverPath = Path.Combine(location, @"bin\win64\vrserver.exe");

                if (!File.Exists(startupPath) || !File.Exists(serverPath))
                {
                    LogError("SteamVR executable(s) do not exist. Has SteamVR been run once?");
                    return null;
                }

                Log($"Found SteamVR paths. Startup: {startupPath}, Server: {serverPath}");
                return new Dictionary<string, string>
                {
                    { "startupPath", startupPath },
                    { "vrServerPath", serverPath }
                };
            }
            catch (Exception e)
            {
                LogError($"Error reading OpenVR Paths file: {e}");
                return null;
            }
        }

        private static void Log(string message)
        {
            File.AppendAllText(LogFilePath, $"{DateTime.Now}: {message}\n");
        }

        private static void LogError(string errorMessage)
        {
            Log($"ERROR: {errorMessage}");
            MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
