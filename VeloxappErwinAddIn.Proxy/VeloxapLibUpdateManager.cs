using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace VeloxapErwinAddIn.Proxy
{
    internal static class VeloxapLibUpdateManager
    {
        private const string DefaultLocalFallbackVersion = "1.0.0.0";
        private const string DefaultServerVersion = "1.1.0.0";
        private const string VersionSettingsFileName = "VeloxapLibUpdateSettings.txt";
        private const string LibraryFileName = "VeloxapEDGEWpfLib.dll";
        private const string UpdateScriptFileName = "UpdateVeloxapLib.ps1";

        // TODO: Change this to the real server/UNC folder that contains the 1.1 library files.
        private const string ServerLibraryPath = @"C:\Users\aykut\Desktop\Workspace\VeloxapEDGEWpf\VeloxapEDGEWpfLib\bin\Debug";

        public static UpdateCheckResult StartUpdateIfRequired()
        {
            var versionSettings = ReadVersionSettings();
            var currentVersion = GetCurrentLibraryVersion(versionSettings.LocalFallbackVersion);
            var serverVersion = ParseVersion(versionSettings.ServerVersion);

            if (currentVersion.CompareTo(serverVersion) >= 0)
                return UpdateCheckResult.NotRequired();

            //string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine("C:\\Users\\aykut\\Desktop\\Workspace\\VeloxapEDGEWpf\\VeloxappErwinAddIn.Proxy", UpdateScriptFileName);

            if (!File.Exists(scriptPath))
            {
                return UpdateCheckResult.RequiredButNotStarted(
                    "A newer VeloxapEDGEWpfLib version is available, but the update script was not found." +
                    Environment.NewLine +
                    "Expected script: " + scriptPath);
            }

            try
            {
                StartUpdateScript(scriptPath, ServerLibraryPath, "C:\\Users\\aykut\\Desktop\\Workspace\\VeloxapEDGEWpf\\VeloxappErwinAddIn.Proxy\\bin\\Debug", Process.GetCurrentProcess().Id);

                return UpdateCheckResult.RequiredAndStarted(
                    "A newer VeloxapEDGEWpfLib version is available." +
                    Environment.NewLine +
                    "Current version: " + currentVersion +
                    Environment.NewLine +
                    "Server version: " + serverVersion +
                    Environment.NewLine +
                    "Erwin will close now. The updater will copy the library files after Erwin exits.");
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.RequiredButNotStarted(
                    "A newer VeloxapEDGEWpfLib version is available, but the updater could not be started." +
                    Environment.NewLine +
                    ex.Message);
            }
        }

        public static void RequestHostApplicationClose()
        {
            try
            {
                Process.GetCurrentProcess().CloseMainWindow();
            }
            catch
            {
                // The updater is already running and will wait for the host process.
            }
        }

        private static Version GetCurrentLibraryVersion(string localFallbackVersion)
        {
            string libPath = Path.Combine("C:\\Users\\aykut\\Desktop\\Workspace\\VeloxapEDGEWpf\\VeloxappErwinAddIn.Proxy\\bin\\Debug", LibraryFileName);

            if (!File.Exists(libPath))
                return ParseVersion(localFallbackVersion);

            try
            {
                var info = FileVersionInfo.GetVersionInfo(libPath);
                if (!string.IsNullOrWhiteSpace(info.FileVersion))
                    return ParseVersion(info.FileVersion);
            }
            catch
            {
            }

            try
            {
                return System.Reflection.AssemblyName.GetAssemblyName(libPath).Version ?? ParseVersion(localFallbackVersion);
            }
            catch
            {
                return ParseVersion(localFallbackVersion);
            }
        }

        private static VersionSettings ReadVersionSettings()
        {
            string settingsPath = Path.Combine("C:\\Users\\aykut\\Desktop\\Workspace\\VeloxapEDGEWpf\\VeloxappErwinAddIn.Proxy\\bin\\Debug", VersionSettingsFileName);

            if (!File.Exists(settingsPath))
            {
                MessageBox.Show("path okundu " + settingsPath);
                return VersionSettings.Default(DefaultLocalFallbackVersion, DefaultServerVersion);
            }

            try
            {
                string[] lines = File.ReadAllLines(settingsPath);
                MessageBox.Show("path okundu " + lines);

                return new VersionSettings(
                    ReadVersionSetting(lines, 0, DefaultLocalFallbackVersion),
                    ReadVersionSetting(lines, 1, DefaultServerVersion));
            }
            catch
            {
                return VersionSettings.Default(DefaultLocalFallbackVersion, DefaultServerVersion);
            }
        }

        private static string ReadVersionSetting(string[] lines, int index, string defaultValue)
        {
            if (lines == null || lines.Length <= index)
                return defaultValue;

            string value = lines[index];
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            int separatorIndex = value.IndexOf('=');
            if (separatorIndex >= 0)
                value = value.Substring(separatorIndex + 1);

            value = value.Trim();
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static void StartUpdateScript(string scriptPath, string sourcePath, string targetPath, int hostProcessId)
        {
            string powershellArguments =
                "-NoExit -NoProfile -ExecutionPolicy Bypass -File " + Quote(scriptPath) +
                " -SourcePath " + Quote(sourcePath) +
                " -TargetPath " + Quote(targetPath) +
                " -HostProcessId " + hostProcessId.ToString(CultureInfo.InvariantCulture);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = powershellArguments,
                CreateNoWindow = false,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            Process.Start(startInfo);
        }

        private static Version ParseVersion(string value)
        {
            Version version;
            return Version.TryParse(value, out version) ? version : new Version(1, 0, 0, 0);
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            var quoted = new System.Text.StringBuilder();
            quoted.Append('"');

            int backslashCount = 0;
            foreach (char current in value)
            {
                if (current == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (current == '"')
                {
                    quoted.Append('\\', backslashCount * 2 + 1);
                    quoted.Append('"');
                    backslashCount = 0;
                    continue;
                }

                quoted.Append('\\', backslashCount);
                backslashCount = 0;
                quoted.Append(current);
            }

            quoted.Append('\\', backslashCount * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
    }

    internal sealed class VersionSettings
    {
        public VersionSettings(string localFallbackVersion, string serverVersion)
        {
            LocalFallbackVersion = localFallbackVersion;
            ServerVersion = serverVersion;
        }

        public string LocalFallbackVersion { get; }

        public string ServerVersion { get; }

        public static VersionSettings Default(string localFallbackVersion, string serverVersion)
        {
            return new VersionSettings(localFallbackVersion, serverVersion);
        }
    }

    internal sealed class UpdateCheckResult
    {
        private UpdateCheckResult(bool updateRequired, bool started, string message)
        {
            UpdateRequired = updateRequired;
            Started = started;
            Message = message ?? string.Empty;
        }

        public bool UpdateRequired { get; }

        public bool Started { get; }

        public string Message { get; }

        public static UpdateCheckResult NotRequired()
        {
            return new UpdateCheckResult(false, false, string.Empty);
        }

        public static UpdateCheckResult RequiredAndStarted(string message)
        {
            return new UpdateCheckResult(true, true, message);
        }

        public static UpdateCheckResult RequiredButNotStarted(string message)
        {
            return new UpdateCheckResult(true, false, message);
        }
    }
}
