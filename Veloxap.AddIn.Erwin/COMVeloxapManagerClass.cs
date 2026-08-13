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
using System.Web.Script.Serialization;
using Veloxap.AddIn.Erwin.Models;
using VeloxapEDGErwinTools.AddIn;

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

            string snapshotPath = CreateModelSnapshot();

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--snapshot \"" + snapshotPath + "\"",
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = false
            });
        }

        private static string CreateModelSnapshot()
        {
            var snapshot = new ExternalModelSnapshot();
            SCAPI.Application application = new SCAPI.Application();
            var erwin = new VeloxapEDGErwinLib(ref application);
            var modelList = erwin.getModelsNamePath() ?? new List<(string value, string key1, string key2)>();

            for (int index = 0; index < modelList.Count; index++)
            {
                var model = modelList[index];
                var snapshotItem = new ExternalModelSnapshotItem
                {
                    DisplayName = model.Item1,
                    ObjectId = model.Item2,
                    PersistenceObjectId = model.Item3,
                    // Used by the Model UDP screen.
                    Model = ModelSnapshotInfo.FromModelInfo(
                        erwin.loadModelObject(model.Item2, model.Item3))
                };

                // Preserve the existing Model & Tablo Bilgileri workflow:
                // getModelObjects() builds the left tree and GetObjectProperties()
                // populates the right detail grid for each selected node.
                snapshotItem.ModelObjects.Add(new ExternalModelObjectSnapshot
                {
                    ClassName = "Model",
                    Name = model.Item1,
                    ObjectId = model.Item2,
                    ParentObjectId = null,
                    IsRoot = true,
                    Properties = erwin.GetObjectProperties(true, model.Item2, null, index)
                });

                var modelObjects = erwin.getModelObjects(model.Item2, index) ??
                    new List<(string, string, string)>();
                foreach (var modelObject in modelObjects)
                {
                    snapshotItem.ModelObjects.Add(new ExternalModelObjectSnapshot
                    {
                        ClassName = modelObject.Item1,
                        Name = modelObject.Item2,
                        ObjectId = modelObject.Item3,
                        ParentObjectId = model.Item2,
                        IsRoot = false,
                        Properties = erwin.GetObjectProperties(
                            false,
                            modelObject.Item3,
                            model.Item2,
                            index)
                    });
                }

                snapshot.Models.Add(new ExternalModelSnapshotItem
                {
                    DisplayName = snapshotItem.DisplayName,
                    ObjectId = snapshotItem.ObjectId,
                    PersistenceObjectId = snapshotItem.PersistenceObjectId,
                    Model = snapshotItem.Model,
                    ModelObjects = snapshotItem.ModelObjects
                });
            }

            string snapshotPath = Path.Combine(
                Path.GetTempPath(),
                "Veloxap-Erwin-" + Guid.NewGuid().ToString("N") + ".json");
            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            };
            File.WriteAllText(snapshotPath, serializer.Serialize(snapshot), Encoding.UTF8);
            return snapshotPath;
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
