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
using Veloxap.AddIn.Erwin.Services;
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
            ClearPreviousLogFiles();

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
                Arguments = "--snapshot \"" + snapshotPath + "\" --owner " + ownerHandle.ToInt64(),
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = false
            });
        }

        private static void ClearPreviousLogFiles()
        {
            try
            {
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Veloxap.AddIn");

                if (!Directory.Exists(logDirectory))
                    return;

                foreach (string logFilePath in Directory.EnumerateFiles(logDirectory, "*.log"))
                {
                    try
                    {
                        File.Delete(logFilePath);
                    }
                    catch
                    {
                        // Log cleanup must never prevent the add-in from starting.
                    }
                }
            }
            catch
            {
                // Log cleanup must never prevent the add-in from starting.
            }
        }

        private static string CreateModelSnapshot()
        {
            var snapshotStopwatch = Stopwatch.StartNew();
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
                    // The full model graph used to be serialized here for every
                    // model. That duplicates the Model & Tablo data below and
                    // makes large models take an excessive time to open. The UI
                    // only needs the model-level summary at startup.
                    Model = ModelSnapshotInfo.FromModelInfo(
                        erwin.loadModelSummary(model.Item2, model.Item3))
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

                // Keep all data exactly as before, but read every entity through
                // one SCAPI session instead of opening one session per entity.
                var propertiesByObjectId = erwin.GetObjectPropertiesBatch(
                    model.Item2,
                    modelObjects.Select(modelObject => modelObject.Item3),
                    index);

                foreach (var modelObject in modelObjects)
                {
                    ObjectPropertiesResult properties;
                    if (!propertiesByObjectId.TryGetValue(modelObject.Item3, out properties))
                        properties = new ObjectPropertiesResult();

                    snapshotItem.ModelObjects.Add(new ExternalModelObjectSnapshot
                    {
                        ClassName = modelObject.Item1,
                        Name = modelObject.Item2,
                        ObjectId = modelObject.Item3,
                        ParentObjectId = model.Item2,
                        IsRoot = false,
                        Properties = properties
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
            snapshotStopwatch.Stop();
            ScapiTraceLogger.Info(
                "UI host snapshot created: models=" + snapshot.Models.Count +
                ", elapsedMs=" + snapshotStopwatch.ElapsedMilliseconds +
                ", path=" + snapshotPath);
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
