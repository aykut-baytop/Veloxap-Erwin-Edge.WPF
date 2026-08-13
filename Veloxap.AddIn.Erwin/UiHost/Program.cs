using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
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
                var window = new Window1
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = true
                };

                window.Init(snapshot);

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
