using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VeloxapEDGEWpfLib;

namespace VeloxapEDGEWpf
{
    [ComVisible(true)]
    [Guid("8F3E7C2A-9B8C-4B0C-9D31-222222222533")]
    [ProgId("VeloxapEDGWPF.AddIn")]
    [ClassInterface(ClassInterfaceType.None)]
    public class COMVeloxapManagerClass : IErwinAddIn
    {
        public COMVeloxapManagerClass() { }

        public void Run()
        {
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
                throw new InvalidOperationException("Veloxap EDGE WPF add-in failed to start.", startupError);
        }

        private static void RunWindow()
        {
            SCAPI.Application app = new SCAPI.Application();

            Window1 mainForm = new Window1();
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
                key?.SetValue("", "Veloxap ErwinTools AddIn");
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
    [Guid("E2B2A1C6-1B2C-4F21-9B71-111111111331")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IErwinAddIn
    {
        void Run();
    }
}
