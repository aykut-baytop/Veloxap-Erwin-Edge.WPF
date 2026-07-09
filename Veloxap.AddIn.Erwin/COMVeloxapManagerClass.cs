using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Veloxap.AddIn.Erwin;

namespace Veloxap.AddIn
{
    [ComVisible(true)]
    [Guid("8F3E7C2A-9B8C-4B0C-9D31-222222233533")]
    [ProgId("VeloxapEDGWPF.AddIn")]
    [ClassInterface(ClassInterfaceType.None)]
    public class COMVeloxapManagerClass : IErwinAddIn
    {
        private static readonly object OpenWindowsLock = new object();
        private static readonly List<Window1> OpenWindows = new List<Window1>();

        public COMVeloxapManagerClass() { }

        public void Run()
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                ShowWindow(shutdownDispatcherOnClose: false);
                return;
            }

            Exception startupError = null;
            bool windowStarted = false;
            var startupCompleted = new ManualResetEventSlim(false);

            var uiThread = new Thread(() =>
            {
                try
                {
                    ShowWindow(shutdownDispatcherOnClose: true);
                    windowStarted = true;
                }
                catch (Exception ex)
                {
                    startupError = ex;
                }
                finally
                {
                    startupCompleted.Set();
                }

                if (windowStarted)
                    Dispatcher.Run();
            });

            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.IsBackground = true;
            uiThread.Start();
            startupCompleted.Wait();

            if (startupError != null)
                throw new InvalidOperationException("Veloxap EDGE WPF add-in failed to start.", startupError);
        }

        private static void ShowWindow(bool shutdownDispatcherOnClose)
        {
            SCAPI.Application app = ScapiApplicationProvider.GetApplication();

            Window1 mainForm = new Window1();
            mainForm.ShowActivated = false;
            mainForm.ShowInTaskbar = false;
            mainForm.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            mainForm.Closed += (sender, args) =>
            {
                lock (OpenWindowsLock)
                {
                    OpenWindows.Remove(mainForm);
                }

                if (shutdownDispatcherOnClose)
                    mainForm.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            };

            lock (OpenWindowsLock)
            {
                OpenWindows.Add(mainForm);
            }

            try
            {
                mainForm.Init(ref app);
                mainForm.Show();
            }
            catch
            {
                lock (OpenWindowsLock)
                {
                    OpenWindows.Remove(mainForm);
                }

                throw;
            }
        }

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            string clsid = $@"CLSID\{{{t.GUID.ToString().ToUpperInvariant()}}}";
            Registry.ClassesRoot.CreateSubKey(clsid + @"\Programmable")?.Close();

            using (var key = Registry.ClassesRoot.OpenSubKey(clsid, true))
            {
                key?.SetValue("", "Veloxap Add-In Erwin");
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            Registry.ClassesRoot.DeleteSubKey(
                $@"CLSID\{{{t.GUID.ToString().ToUpperInvariant()}}}\Programmable",
                false);
        }
    }

    [ComVisible(true)]
    [Guid("E2B2A1C6-1B2C-4F21-9B71-111111113331")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IErwinAddIn
    {
        void Run();
    }
}
