using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Veloxap.AddIn
{
    [ComVisible(true)]
    [Guid("8F3E7C2A-9B8C-4B0C-9D31-222222233533")]
    [ProgId("VeloxapEDGWPF.AddIn")]
    [ClassInterface(ClassInterfaceType.None)]
    public class COMVeloxapManagerClass : IErwinAddIn
    {
        private const string UiHostExecutableName = "Veloxap.AddIn.Erwin.UiHost.exe";

        public COMVeloxapManagerClass() { }

        public void Run()
        {
            IntPtr ownerHandle = GetHostOwnerHandle();
            string executablePath = Path.Combine(
                Path.GetDirectoryName(typeof(COMVeloxapManagerClass).Assembly.Location),
                UiHostExecutableName);

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException(
                    "WPF UI host executable was not found. Deploy '" + UiHostExecutableName +
                    "' next to the add-in DLL.",
                    executablePath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--erwin-process-id " + Process.GetCurrentProcess().Id +
                            " --owner-hwnd " + ownerHandle.ToInt64(),
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = false
            });
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
