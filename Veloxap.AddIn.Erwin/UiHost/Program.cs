using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using Veloxap.AddIn.Erwin;
using Veloxap.AddIn.Erwin.Models;

namespace Veloxap.AddIn.Erwin.UiHost
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // This runs in a separate process, before WPF is initialized.
            // It intentionally matches the legacy 96-DPI behavior observed in
            // the WinForms add-in, without changing Erwin's process at all.
            SetLegacyDpiAwareness();

            try
            {
                ExternalModelSnapshot snapshot = ReadSnapshot(args);
                IntPtr ownerHandle = ReadOwnerHandle(args);
                var window = new Window1
                {
                    // An owned window is kept above its Erwin owner, closes with it,
                    // and is not represented as a separate taskbar application.
                    WindowStartupLocation = ownerHandle == IntPtr.Zero
                        ? WindowStartupLocation.CenterScreen
                        : WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false
                };

                window.SourceInitialized += delegate
                {
                    RemoveMinimizeButton(window);
                };

                if (ownerHandle != IntPtr.Zero && IsWindow(ownerHandle))
                    new WindowInteropHelper(window).Owner = ownerHandle;

                window.Init(snapshot);

                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose
                };

                // The UI runs in a separate process for DPI isolation. Disable the
                // Erwin owner while it is open so this window behaves as a modal
                // part of the host application rather than a parallel application.
                bool restoreOwnerWhenClosed = ownerHandle != IntPtr.Zero
                    && IsWindow(ownerHandle)
                    && IsWindowEnabled(ownerHandle);

                if (restoreOwnerWhenClosed)
                    EnableWindow(ownerHandle, false);

                try
                {
                    application.Run(window);
                }
                finally
                {
                    if (restoreOwnerWhenClosed && IsWindow(ownerHandle))
                    {
                        EnableWindow(ownerHandle, true);

                        // The owned window is the foreground window while it is
                        // closing. Explicitly restore/activate Erwin so Windows
                        // does not leave its owner minimized on the taskbar.
                        if (IsIconic(ownerHandle))
                            ShowWindowAsync(ownerHandle, SwRestore);

                        SetForegroundWindow(ownerHandle);
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Veloxap Erwin Add-In arayuzu baslatilamadi.\n\n" + exception,
                    "Veloxap Erwin Add-In",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static ExternalModelSnapshot ReadSnapshot(string[] args)
        {
            string snapshotPath = GetArgumentValue(args, "--snapshot");
            if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath))
                throw new FileNotFoundException("Erwin model snapshot dosyasi bulunamadi.", snapshotPath);

            string json = File.ReadAllText(snapshotPath);
            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            };
            var snapshot = serializer.Deserialize<ExternalModelSnapshot>(json);

            try
            {
                File.Delete(snapshotPath);
            }
            catch
            {
            }

            return snapshot ?? new ExternalModelSnapshot();
        }

        private static string GetArgumentValue(string[] args, string name)
        {
            if (args == null)
                return null;

            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            }

            return null;
        }

        private static IntPtr ReadOwnerHandle(string[] args)
        {
            long ownerValue;
            return long.TryParse(GetArgumentValue(args, "--owner"), out ownerValue)
                ? new IntPtr(ownerValue)
                : IntPtr.Zero;
        }

        private static void SetLegacyDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_UNAWARE. This only affects the UI host
                // process; Erwin is a different process and remains unchanged.
                SetProcessDpiAwarenessContext(new IntPtr(-1));
            }
            catch (EntryPointNotFoundException)
            {
                // The process is still separate from Erwin on older Windows.
            }
        }

        private static void RemoveMinimizeButton(Window window)
        {
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;
            if (windowHandle == IntPtr.Zero)
                return;

            long windowStyle = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
            SetWindowLongPtr(
                windowHandle,
                GwlStyle,
                new IntPtr(windowStyle & ~WsMinimizeBox));
            SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
        }

        private const int GwlStyle = -16;
        private const long WsMinimizeBox = 0x00020000L;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpFrameChanged = 0x0020;
        private const int SwRestore = 9;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool enable);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);
    }
}
