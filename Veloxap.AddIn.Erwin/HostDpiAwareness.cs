using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Veloxap.AddIn.Erwin
{
    internal static class HostDpiAwareness
    {
        private const int AccessDenied = unchecked((int)0x80070005);

        internal static void Preserve()
        {
            // PresentationCore calls SetProcessDPIAware when its module loads.
            // In a native COM host, DisableDpiAwareness on this DLL is ignored:
            // WPF checks the entry assembly, which is null for erwin.
            // Explicitly set the CURRENT level so WPF cannot later promote an
            // unaware host to system-aware and change its window dimensions.
            using (var process = Process.GetCurrentProcess())
            {
                ProcessDpiAwareness awareness;
                Marshal.ThrowExceptionForHR(GetProcessDpiAwareness(process.Handle, out awareness));

                int result = SetProcessDpiAwareness(awareness);
                // A manifest or an earlier API call already fixed the level.
                if (result != AccessDenied)
                    Marshal.ThrowExceptionForHR(result);
            }
        }

        private enum ProcessDpiAwareness
        {
            Unaware = 0,
            SystemAware = 1,
            PerMonitorAware = 2
        }

        [DllImport("shcore.dll")]
        private static extern int GetProcessDpiAwareness(IntPtr process, out ProcessDpiAwareness awareness);

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(ProcessDpiAwareness awareness);
    }
}
