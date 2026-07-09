using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Veloxap.AddIn.Erwin.Services
{
    internal static class PerformanceTraceLogger
    {
        private static readonly object SyncRoot = new object();

        public static string LogFilePath
        {
            get
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Veloxap.AddIn");

                return Path.Combine(directory, "scapi-performance.log");
            }
        }

        public static PerformanceTraceScope Start(string operation)
        {
            return Start(operation, null);
        }

        public static PerformanceTraceScope Start(string operation, string detail)
        {
            var scope = new PerformanceTraceScope(operation, detail);
            Write("START", operation, detail, null, null, null);
            return scope;
        }

        public static void Info(string operation, string detail)
        {
            Write("INFO", operation, detail, null, null, null);
        }

        public static void Error(string operation, string detail, Exception exception)
        {
            Write("ERROR", operation, detail, null, null, exception);
        }

        public static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value.Substring(0, maxLength) + "...";
        }

        private static void Complete(
            string operation,
            string detail,
            TimeSpan elapsed,
            string result,
            Exception exception)
        {
            Write(
                exception == null ? "END" : "END-ERROR",
                operation,
                detail,
                elapsed,
                result,
                exception);
        }

        private static void Write(
            string level,
            string operation,
            string detail,
            TimeSpan? elapsed,
            string result,
            Exception exception)
        {
            try
            {
                string directory = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string entry =
                    "==================================================" + Environment.NewLine +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    " [" + level + "]" + Environment.NewLine +
                    "Operation: " + (operation ?? string.Empty) + Environment.NewLine +
                    "ThreadId: " + Thread.CurrentThread.ManagedThreadId + Environment.NewLine;

                if (elapsed.HasValue)
                {
                    entry += "ElapsedMs: " +
                             elapsed.Value.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) +
                             Environment.NewLine;
                }

                if (!string.IsNullOrWhiteSpace(detail))
                    entry += "Detail: " + detail + Environment.NewLine;

                if (!string.IsNullOrWhiteSpace(result))
                    entry += "Result: " + result + Environment.NewLine;

                if (exception != null)
                    entry += exception + Environment.NewLine;

                lock (SyncRoot)
                {
                    File.AppendAllText(LogFilePath, entry);
                }
            }
            catch
            {
                // Performance logging must never break the add-in flow.
            }
        }

        internal sealed class PerformanceTraceScope : IDisposable
        {
            private readonly Stopwatch stopwatch;
            private readonly string operation;
            private readonly string detail;
            private string result;
            private Exception exception;
            private bool disposed;

            internal PerformanceTraceScope(string operation, string detail)
            {
                this.operation = operation;
                this.detail = detail;
                stopwatch = Stopwatch.StartNew();
            }

            public void SetResult(string value)
            {
                result = value;
            }

            public void Fail(Exception ex)
            {
                exception = ex;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                stopwatch.Stop();
                Complete(operation, detail, stopwatch.Elapsed, result, exception);
            }
        }
    }
}
