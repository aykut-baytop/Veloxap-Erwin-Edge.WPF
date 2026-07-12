using System;
using System.Diagnostics;
using System.IO;

namespace Veloxap.AddIn.Erwin.Services
{
    internal static class ScapiTraceLogger
    {
        private const long SlowStepThresholdMilliseconds = 250;
        private static readonly object SyncRoot = new object();

        public static string LogFilePath
        {
            get
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Veloxap.AddIn");

                return Path.Combine(directory, "scapi-trace.log");
            }
        }

        public static Stopwatch StartTimer()
        {
            return Stopwatch.StartNew();
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        public static void Step(string operation, Stopwatch timer, string details)
        {
            WriteDuration("INFO", operation, timer, details);
        }

        public static void SlowStep(string operation, Stopwatch timer, string details)
        {
            if (!IsSlow(timer))
                return;

            WriteDuration("WARN", operation, timer, details);
        }

        public static bool IsSlow(Stopwatch timer)
        {
            return timer != null && timer.ElapsedMilliseconds >= SlowStepThresholdMilliseconds;
        }

        public static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value.Substring(0, maxLength) + "...";
        }

        private static void WriteDuration(
            string level,
            string operation,
            Stopwatch timer,
            string details)
        {
            long elapsedMilliseconds = timer == null ? 0 : timer.ElapsedMilliseconds;

            Write(
                level,
                (operation ?? "SCAPI STEP") + Environment.NewLine +
                "ElapsedMs: " + elapsedMilliseconds + Environment.NewLine +
                (details ?? string.Empty),
                null);
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
