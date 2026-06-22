using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using VeloxapEDGEWpfLib;
using System.Windows;

namespace VeloxapErwinAddIn.Proxy
{
    [ComVisible(true)]
    [Guid("8F3E7C2A-9B8C-4B0C-9D31-222222222543")]
    [ProgId("VeloxapErwinAddIn.Proxy")]
    [ClassInterface(ClassInterfaceType.None)]
    public class COMVeloxapManagerClass : IErwinAddIn
    {
        public COMVeloxapManagerClass() { }

        public void Run()
        {
            var updateResult = VeloxapLibUpdateManager.StartUpdateIfRequired();
            if (updateResult.UpdateRequired)
            {
                MessageBox.Show(
                    updateResult.Message,
                    "Veloxap ErwinTools Update",
                    MessageBoxButton.OK,
                    updateResult.Started ? MessageBoxImage.Information : MessageBoxImage.Warning);

                if (updateResult.Started)
                    VeloxapLibUpdateManager.RequestHostApplicationClose();

                return;
            }

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                RunWindow();
                return;
            }

            Exception startupError = null;
            var uiThread = new Thread(() =>
            {
                try
                {
                    RunWindow();
                }
                catch (Exception ex)
                {
                    startupError = ex;
                }
            });

            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.IsBackground = false;
            uiThread.Start();
            uiThread.Join();

            if (startupError != null)
                throw new InvalidOperationException("Veloxap ErwinTools AddIn failed to start.", startupError);
        }

        private static void RunWindow()
        {
            var app = new SCAPI.Application();

            var mainForm = new Window1();
            mainForm.Init(ref app);
            mainForm.ShowDialog();
        }

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            string clsid = $@"CLSID\{{{t.GUID.ToString().ToUpperInvariant()}}}";
            Registry.ClassesRoot.CreateSubKey(clsid + @"\Programmable")?.Close();

            using (var key = Registry.ClassesRoot.OpenSubKey(clsid, true))
            {
                key?.SetValue("", "Veloxap ErwinTools AddIn Proxy");
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
    [Guid("E2B2A1C6-1B2C-4F21-9B71-111111122331")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IErwinAddIn
    {
        void Run();
    }
}
