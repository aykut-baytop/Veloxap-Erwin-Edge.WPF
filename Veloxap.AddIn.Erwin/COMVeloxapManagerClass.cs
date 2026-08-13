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
            Exception startupError = null;

            // WPF's Dispatcher and ShowDialog modal loop change the current
            // thread's DPI context while processing messages for its window.
            // Running that loop on Erwin's STA thread is what made Erwin move
            // to the top-left and appear scaled. Always isolate WPF in its own
            // STA thread; the native Owner handle still preserves modality and
            // Z-order relative to Erwin.
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

            // This is the add-in's dedicated STA thread, never Erwin's UI
            // thread. Keeping the scope around the WPF modal loop produces a
            // legacy 96-DPI WPF window without virtualizing Erwin's messages.
            DpiDiagnostics.Log("before WPF DPI scope", IntPtr.Zero, ownerHandle);

            using (DpiAwarenessScope.EnterUnaware())
            {
                DpiDiagnostics.Log("inside WPF DPI scope", IntPtr.Zero, ownerHandle);

                Window1 mainForm = new Window1();
                ConfigureIndependentWindow(mainForm, ownerHandle);
                mainForm.SourceInitialized += (sender, args) => DpiDiagnostics.Log(
                    "WPF main window source initialized",
                    new WindowInteropHelper(mainForm).Handle,
                    ownerHandle);
                mainForm.Init(ref app);
                mainForm.ShowDialog();
            }
        }

        private static void ConfigureIndependentWindow(Window window, IntPtr ownerHandle)
        {
            if (window == null)
                return;

            // Do not set WindowInteropHelper.Owner here. Erwin's system-aware
            // HWND is 120 DPI while this legacy add-in window is 96 DPI. The
            // mixed-DPI native ownership relationship makes Erwin re-layout
            // itself (top-left, reduced size). Run() waits until ShowDialog
            // closes, so Erwin cannot receive another add-in invocation while
            // this independent dialog is displayed.
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            DpiDiagnostics.Log("native owner intentionally omitted", IntPtr.Zero, ownerHandle);
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
