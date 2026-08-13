using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Veloxap.AddIn.Erwin.Services;

namespace Veloxap.AddIn
{
    /// <summary>
    /// Writes DPI information to the existing add-in trace log. All native
    /// calls are best-effort so diagnostics can never prevent the add-in from
    /// opening on an older Windows version.
    /// </summary>
    internal static class DpiDiagnostics
    {
        internal static void Log(string point, IntPtr addInWindow, IntPtr ownerWindow)
        {
            try
            {
                var message = new StringBuilder();
                message.AppendLine("DPI diagnostics: " + point);
                message.AppendLine("Process id: " + Process.GetCurrentProcess().Id);
                message.AppendLine("Process awareness: " + GetProcessAwareness());
                message.AppendLine("Thread awareness: " + GetThreadAwareness());
                message.AppendLine("System DPI: " + GetSystemDpi());
                AppendWindow(message, "Erwin owner", ownerWindow);
                AppendWindow(message, "WPF add-in", addInWindow);

                IntPtr foregroundWindow = GetForegroundWindow();
                AppendWindow(message, "Foreground", foregroundWindow);
                ApiTraceLogger.Info(message.ToString());
            }
            catch
            {
                // Diagnostics must never affect the add-in workflow.
            }
        }

        private static void AppendWindow(StringBuilder message, string name, IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                message.AppendLine(name + ": none");
                return;
            }

            uint processId;
            GetWindowThreadProcessId(window, out processId);
            message.AppendLine(
                name + ": hwnd=0x" + window.ToInt64().ToString("X") +
                ", process=" + processId +
                ", dpi=" + GetWindowDpi(window) +
                ", awareness=" + GetWindowAwareness(window));
        }

        private static string GetProcessAwareness()
        {
            try
            {
                int awareness;
                int result = GetProcessDpiAwareness(IntPtr.Zero, out awareness);
                return result == 0 ? AwarenessName(awareness) : "unavailable (" + result + ")";
            }
            catch (DllNotFoundException)
            {
                return "unavailable";
            }
            catch (EntryPointNotFoundException)
            {
                return "unavailable";
            }
        }

        private static string GetThreadAwareness()
        {
            try
            {
                IntPtr context = GetThreadDpiAwarenessContext();
                return AwarenessName(GetAwarenessFromDpiAwarenessContext(context)) +
                       " (context=0x" + context.ToInt64().ToString("X") + ")";
            }
            catch (EntryPointNotFoundException)
            {
                return "unavailable";
            }
        }

        private static uint GetSystemDpi()
        {
            try
            {
                return GetDpiForSystem();
            }
            catch (EntryPointNotFoundException)
            {
                return 0;
            }
        }

        private static uint GetWindowDpi(IntPtr window)
        {
            try
            {
                return GetDpiForWindow(window);
            }
            catch (EntryPointNotFoundException)
            {
                return 0;
            }
        }

        private static string GetWindowAwareness(IntPtr window)
        {
            try
            {
                return AwarenessName(GetAwarenessFromDpiAwarenessContext(
                    GetWindowDpiAwarenessContext(window)));
            }
            catch (EntryPointNotFoundException)
            {
                return "unavailable";
            }
        }

        private static string AwarenessName(int awareness)
        {
            switch (awareness)
            {
                case 0: return "PROCESS_DPI_UNAWARE";
                case 1: return "PROCESS_SYSTEM_DPI_AWARE";
                case 2: return "PROCESS_PER_MONITOR_DPI_AWARE";
                default: return "unknown (" + awareness + ")";
            }
        }

        [DllImport("shcore.dll")]
        private static extern int GetProcessDpiAwareness(IntPtr process, out int awareness);

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
