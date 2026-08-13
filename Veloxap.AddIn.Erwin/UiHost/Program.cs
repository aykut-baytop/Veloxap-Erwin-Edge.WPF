using System;
using System.Runtime.InteropServices;
using System.Windows;
using Veloxap.AddIn.Erwin;

namespace Veloxap.AddIn.Erwin.UiHost
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // This runs in a separate process, before WPF is initialized.
            // It intentionally matches the legacy 96-DPI behavior observed in
            // the WinForms add-in, without changing Erwin's process at all.
            SetLegacyDpiAwareness();

            try
            {
                SCAPI.Application app = new SCAPI.Application();
                var window = new Window1
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = true
                };

                window.Init(ref app);

                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose
                };
                application.Run(window);
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
    }
}
