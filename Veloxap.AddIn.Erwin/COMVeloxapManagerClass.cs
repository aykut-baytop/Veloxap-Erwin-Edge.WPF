using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Veloxap.AddIn.Erwin;

namespace Veloxap.AddIn
{
    [ComVisible(true)]
    [Guid("8F3E7C2A-9B8C-4B0C-9D31-222222233533")]
    [ProgId("VeloxapEDGWPF.AddIn")]
    [ClassInterface(ClassInterfaceType.None)]
    public class COMVeloxapManagerClass : IErwinAddIn
    {
        public COMVeloxapManagerClass() { }

        public void Run()
        {
            IntPtr ownerHandle = GetHostOwnerHandle();

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                RunWindow(ownerHandle);
                return;
            }

            Exception startupError = null;
            var uiThread = new Thread(() =>
            {
                try
                {
                    RunWindow(ownerHandle);
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

        private static void RunWindow(IntPtr ownerHandle)
        {
            SCAPI.Application app = new SCAPI.Application();

            // The add-in must use the same SYSTEM_DPI_AWARE context as Erwin.
            // A 96-DPI WPF window within this host process causes Erwin itself
            // to be DPI-virtualized and re-positioned. Do not override the
            // current thread's DPI awareness here.
            Window1 mainForm = new Window1();
            AssignOwner(mainForm, ownerHandle);
            mainForm.Init(ref app);
            mainForm.ShowDialog();
        }

        private static void AssignOwner(Window window, IntPtr ownerHandle)
        {
            if (window == null || ownerHandle == IntPtr.Zero)
                return;

            new WindowInteropHelper(window).Owner = ownerHandle;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private static IntPtr GetHostOwnerHandle()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return IntPtr.Zero;

            uint windowProcessId;
            GetWindowThreadProcessId(foregroundWindow, out windowProcessId);

            return windowProcessId == (uint)Process.GetCurrentProcess().Id
                ? foregroundWindow
                : IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

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
