using System;
using System.IO;

namespace Veloxap.AddIn.Erwin.Services
{
    internal static class ApiTraceLogger
    {
        private static readonly object SyncRoot = new object();

        public static string LogFilePath
        {
            get
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Veloxap.AddIn");

                return Path.Combine(directory, "api-trace.log");
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        public static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value.Substring(0, maxLength) + "...";
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                string directory = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string entry =
                    "==================================================" + Environment.NewLine +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "]" + Environment.NewLine +
                    (message ?? string.Empty) + Environment.NewLine;

                if (exception != null)
                    entry += exception + Environment.NewLine;

                lock (SyncRoot)
                {
                    File.AppendAllText(LogFilePath, entry);
                }
            }
            catch
            {
                // Logging must never break the add-in flow.
            }
        }
    }
}
