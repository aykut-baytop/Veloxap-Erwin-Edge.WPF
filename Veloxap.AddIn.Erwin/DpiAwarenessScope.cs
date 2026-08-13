using System;
using System.Runtime.InteropServices;

namespace Veloxap.AddIn
{
    /// <summary>
    /// Temporarily puts the current UI thread in the legacy DPI-unaware mode.
    /// This is intentionally thread-scoped: the add-in is loaded in Erwin's
    /// process and must not alter the DPI awareness of the host process.
    /// </summary>
    internal sealed class DpiAwarenessScope : IDisposable
    {
        // DPI_AWARENESS_CONTEXT_UNAWARE. Windows applies 96-DPI virtualization
        // to windows that are created while this context is active.
        private static readonly IntPtr UnawareContext = new IntPtr(-1);

        private IntPtr previousContext;
        private bool restoreRequired;

        private DpiAwarenessScope()
        {
        }

        public static DpiAwarenessScope EnterUnaware()
        {
            var scope = new DpiAwarenessScope();

            try
            {
                scope.previousContext = SetThreadDpiAwarenessContext(UnawareContext);
                scope.restoreRequired = scope.previousContext != IntPtr.Zero;
            }
            catch (EntryPointNotFoundException)
            {
                // SetThreadDpiAwarenessContext requires Windows 10 version 1607+
                // (or a newer compatible Windows version).
            }
            catch (DllNotFoundException)
            {
                // Kept for compatibility with unsupported Windows environments.
            }

            return scope;
        }

        public void Dispose()
        {
            if (!restoreRequired)
                return;

            restoreRequired = false;

            try
            {
                SetThreadDpiAwarenessContext(previousContext);
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    }
}
