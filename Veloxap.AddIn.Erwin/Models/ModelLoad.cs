using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Veloxap.AddIn.Erwin.Services;

namespace Veloxap.AddIn.Erwin.Models
{
    internal class ModelLoad
    {
        private const long SlowPropertyPartThresholdMilliseconds = 100;

        private static readonly string[] DefaultObjectClasses =
        {
            "Entity",
            "Relationship",
            "Attribute",
            "Sequence",
            "Key_Group",
            "Key_Group_Member"
        };

        private readonly SCAPI.Application oApplication;

        public ModelLoad(ref SCAPI.Application oApp)
        {
            oApplication = oApp;
        }

        public ModelLoad(SCAPI.Application oApp)
        {
            oApplication = oApp;
        }

        public ModelLoad()
        {
            oApplication = new SCAPI.Application();
        }

        public List<ModelObject> loadTableSummaries(SCAPI.PersistenceUnit oPersistenceUnit)
        {
            var tables = new List<ModelObject>();
            var stats = new ScapiLoadStats("TableSummaries");

            using (var trace = PerformanceTraceLogger.Start(
                "ModelLoad.loadTableSummaries",
                DescribePersistenceUnit(oPersistenceUnit)))
            {
                if (oPersistenceUnit == null || oApplication == null)
                {
                    trace.SetResult("Skipped=True; Reason=PersistenceUnitOrApplicationNull");
                    return tables;
                }

                SCAPI.Session session = null;

                try
                {
                    session = OpenSession(oApplication, oPersistenceUnit, "ModelLoad.loadTableSummaries");
                    SCAPI.ModelObject root = ReadRoot(session, "ModelLoad.loadTableSummaries");

                    foreach (SCAPI.ModelObject entity in Collect(
                        session,
                        root,
                        "Entity",
                        "ModelLoad.loadTableSummaries",
                        stats))
                    {
                        tables.Add(CreateModelObject(entity, stats));
                    }

                    trace.SetResult("TableCount=" + tables.Count + "; " + stats.ToSummary());
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    PerformanceTraceLogger.Error(
                        "ModelLoad.loadTableSummaries",
                        DescribePersistenceUnit(oPersistenceUnit),
                        ex);
                }
                finally
                {
                    CloseSession(oApplication, session, "ModelLoad.loadTableSummaries");
                }

                return tables;
            }
        }

        public ModelObject loadTableUdpObject(
            SCAPI.PersistenceUnit oPersistenceUnit,
            string tableObjectId,
            string tableName)
        {
            var stats = new ScapiLoadStats("TableUdpObject");
            string detail =
                DescribePersistenceUnit(oPersistenceUnit) +
                "; TableObjectId=" + CleanLogValue(tableObjectId) +
                "; TableName=" + CleanLogValue(tableName);

            using (var trace = PerformanceTraceLogger.Start("ModelLoad.loadTableUdpObject", detail))
            {
                if (oPersistenceUnit == null || oApplication == null)
                {
                    trace.SetResult("Skipped=True; Reason=PersistenceUnitOrApplicationNull");
                    return null;
                }

                SCAPI.Session session = null;

                try
                {
                    session = OpenSession(oApplication, oPersistenceUnit, "ModelLoad.loadTableUdpObject");

                    SCAPI.ModelObject entity = FindEntity(
                        session,
                        tableObjectId,
                        tableName,
                        stats);

                    if (entity == null)
                    {
                        trace.SetResult("EntityFound=False; " + stats.ToSummary());
                        return null;
                    }

                    ModelObject table = CreateModelObject(entity, stats);
                    List<ObjectProperty> properties = ReadObjectProperties(
                        entity,
                        "ModelLoad.loadTableUdpObject.Table",
                        stats);
                    List<ModelObject> children = LoadChildObjects(
                        session,
                        entity,
                        new[] { "Attribute" },
                        1,
                        "ModelLoad.loadTableUdpObject.Attributes",
                        stats);

                    table.setoObjectProperty(properties);
                    table.setoModelObjects(children);

                    trace.SetResult(
                        "EntityFound=True; PropertyCount=" + properties.Count +
                        "; ChildCount=" + children.Count +
                        "; " + stats.ToSummary());

                    return table;
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    PerformanceTraceLogger.Error("ModelLoad.loadTableUdpObject", detail, ex);
                    return null;
                }
                finally
                {
                    CloseSession(oApplication, session, "ModelLoad.loadTableUdpObject");
                }
            }
        }

        public ModelInfo loadModelSummary(SCAPI.PersistenceUnit oPersistenceUnit)
        {
            return LoadModel(oPersistenceUnit, ModelLoadMode.Summary);
        }

        public ModelInfo loadTableUdpModel(SCAPI.PersistenceUnit oPersistenceUnit)
        {
            return LoadModel(oPersistenceUnit, ModelLoadMode.TableUdpsOnly);
        }

        public ModelInfo loadModel(SCAPI.PersistenceUnit oPersistenceUnit)
        {
            return LoadModel(oPersistenceUnit, ModelLoadMode.Full);
        }

        private ModelInfo LoadModel(
            SCAPI.PersistenceUnit oPersistenceUnit,
            ModelLoadMode mode)
        {
            var model = new ModelInfo();
            var stats = new ScapiLoadStats(mode.ToString());

            using (var trace = PerformanceTraceLogger.Start(
                "ModelLoad.LoadModel",
                "Mode=" + mode + "; " + DescribePersistenceUnit(oPersistenceUnit)))
            {
                if (oPersistenceUnit == null || oApplication == null)
                {
                    trace.SetResult("Skipped=True; Reason=PersistenceUnitOrApplicationNull");
                    return model;
                }

                SCAPI.Session session = null;

                try
                {
                    session = OpenSession(oApplication, oPersistenceUnit, "ModelLoad.LoadModel." + mode);

                    SCAPI.ModelObject root = ReadRoot(session, "ModelLoad.LoadModel." + mode);
                    ReadModelHeader(model, oPersistenceUnit, root, mode);

                    List<ObjectProperty> rootProperties = ReadObjectProperties(
                        root,
                        "ModelLoad.LoadModel.Root",
                        stats);
                    model.setoObjectProperty(rootProperties);

                    if (mode == ModelLoadMode.Summary)
                    {
                        trace.SetResult(
                            "Mode=" + mode +
                            "; RootPropertyCount=" + rootProperties.Count +
                            "; " + stats.ToSummary());
                        return model;
                    }

                    List<ModelObject> objects = mode == ModelLoadMode.TableUdpsOnly
                        ? LoadTableUdpObjects(session, root, stats)
                        : LoadChildObjects(
                            session,
                            root,
                            DefaultObjectClasses,
                            2,
                            "ModelLoad.LoadModel.Full",
                            stats);

                    model.setoModelObject(objects);

                    trace.SetResult(
                        "Mode=" + mode +
                        "; RootPropertyCount=" + rootProperties.Count +
                        "; TopLevelObjectCount=" + objects.Count +
                        "; " + stats.ToSummary());

                    return model;
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    PerformanceTraceLogger.Error(
                        "ModelLoad.LoadModel",
                        "Mode=" + mode + "; " + DescribePersistenceUnit(oPersistenceUnit),
                        ex);
                    return model;
                }
                finally
                {
                    CloseSession(oApplication, session, "ModelLoad.LoadModel." + mode);
                }
            }
        }

        private static List<ModelObject> LoadTableUdpObjects(
            SCAPI.Session session,
            SCAPI.ModelObject root,
            ScapiLoadStats stats)
        {
            var tables = new List<ModelObject>();

            using (var trace = PerformanceTraceLogger.Start(
                "ModelLoad.LoadTableUdpObjects",
                DescribeModelObject(root)))
            {
                foreach (SCAPI.ModelObject entity in Collect(
                    session,
                    root,
                    "Entity",
                    "ModelLoad.LoadTableUdpObjects",
                    stats))
                {
                    ModelObject table = CreateModelObject(entity, stats);
                    table.setoObjectProperty(ReadObjectProperties(
                        entity,
                        "ModelLoad.LoadTableUdpObjects.Table",
                        stats));
                    table.setoModelObjects(LoadChildObjects(
                        session,
                        entity,
                        new[] { "Attribute" },
                        1,
                        "ModelLoad.LoadTableUdpObjects.Attributes",
                        stats));
                    tables.Add(table);
                }

                trace.SetResult("TableCount=" + tables.Count + "; " + stats.ToSummary());
                return tables;
            }
        }

        private static List<ModelObject> LoadChildObjects(
            SCAPI.Session session,
            SCAPI.ModelObject parent,
            string[] allowedClasses,
            int remainingDepth,
            string caller,
            ScapiLoadStats stats)
        {
            var modelObjects = new List<ModelObject>();

            if (stats != null)
                stats.ChildLoadCalls++;

            string detail =
                "Caller=" + CleanLogValue(caller) +
                "; RemainingDepth=" + remainingDepth +
                "; AllowedClasses=" + CleanLogValue(FormatAllowedClasses(allowedClasses)) +
                "; " + DescribeModelObject(parent);

            using (var trace = PerformanceTraceLogger.Start("ModelLoad.LoadChildObjects", detail))
            {
                if (session == null || parent == null || remainingDepth <= 0)
                {
                    trace.SetResult("Skipped=True; ResultCount=0");
                    return modelObjects;
                }

                foreach (SCAPI.ModelObject scapiObject in Collect(
                    session,
                    parent,
                    null,
                    caller,
                    stats))
                {
                    if (!IsAllowedClass(scapiObject, allowedClasses))
                        continue;

                    ModelObject modelObject = CreateModelObject(scapiObject, stats);
                    modelObject.setoObjectProperty(ReadObjectProperties(
                        scapiObject,
                        "ModelLoad.LoadChildObjects.Properties",
                        stats));
                    modelObject.setoModelObjects(LoadChildObjects(
                        session,
                        scapiObject,
                        allowedClasses,
                        remainingDepth - 1,
                        "ModelLoad.LoadChildObjects.Children",
                        stats));
                    modelObjects.Add(modelObject);
                }

                trace.SetResult("ResultCount=" + modelObjects.Count + "; " + stats.ToSummary());
                return modelObjects;
            }
        }

        private static List<SCAPI.ModelObject> Collect(
            SCAPI.Session session,
            SCAPI.ModelObject parent,
            string className,
            string caller,
            ScapiLoadStats stats)
        {
            var objects = new List<SCAPI.ModelObject>();

            if (stats != null)
                stats.CollectCalls++;

            string parentObjectId = SafeGetString(() => parent.ObjectId);
            string detail =
                "Caller=" + CleanLogValue(caller) +
                "; ClassName=" + CleanLogValue(className ?? "<all>") +
                "; ParentObjectId=" + CleanLogValue(parentObjectId) +
                "; " + DescribeModelObject(parent);

            using (var trace = PerformanceTraceLogger.Start("ModelLoad.Collect", detail))
            {
                if (session == null || parent == null)
                {
                    trace.SetResult("Skipped=True; ObjectCount=0");
                    return objects;
                }

                try
                {
                    SCAPI.ModelObjects selectedCollection = null;

                    using (var collectTrace = PerformanceTraceLogger.Start(
                        "SCAPI.ModelObjects.Collect",
                        detail))
                    {
                        selectedCollection = session.ModelObjects.Collect(
                            parentObjectId,
                            className,
                            1);
                        collectTrace.SetResult("CollectionCreated=True");
                    }

                    using (var enumerateTrace = PerformanceTraceLogger.Start(
                        "SCAPI.ModelObjects.Collect.Enumerate",
                        detail))
                    {
                        foreach (SCAPI.ModelObject scapiObject in selectedCollection)
                            objects.Add(scapiObject);

                        enumerateTrace.SetResult("ObjectCount=" + objects.Count);
                    }

                    if (stats != null)
                        stats.CollectedObjects += objects.Count;

                    trace.SetResult("ObjectCount=" + objects.Count);
                }
                catch (Exception ex)
                {
                    if (stats != null)
                        stats.CollectErrors++;

                    trace.Fail(ex);
                    PerformanceTraceLogger.Error("ModelLoad.Collect", detail, ex);
                }

                return objects;
            }
        }

        private static SCAPI.ModelObject FindEntity(
            SCAPI.Session session,
            string tableObjectId,
            string tableName,
            ScapiLoadStats stats)
        {
            string detail =
                "TableObjectId=" + CleanLogValue(tableObjectId) +
                "; TableName=" + CleanLogValue(tableName);

            using (var trace = PerformanceTraceLogger.Start("ModelLoad.FindEntity", detail))
            {
                if (session == null)
                {
                    trace.SetResult("Skipped=True; Reason=SessionNull");
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(tableObjectId))
                {
                    using (var lookupTrace = PerformanceTraceLogger.Start(
                        "SCAPI.ModelObjects.ItemByObjectId",
                        detail))
                    {
                        try
                        {
                            SCAPI.ModelObject directEntity = session.ModelObjects[tableObjectId];
                            lookupTrace.SetResult("Found=" + (directEntity != null));

                            if (directEntity != null)
                            {
                                trace.SetResult("Found=True; FoundBy=ObjectId");
                                return directEntity;
                            }
                        }
                        catch (Exception ex)
                        {
                            lookupTrace.Fail(ex);
                            PerformanceTraceLogger.Info(
                                "ModelLoad.FindEntity.DirectLookupMiss",
                                detail + "; Error=" + CleanLogValue(ex.Message));
                        }
                    }
                }

                try
                {
                    SCAPI.ModelObject root = ReadRoot(session, "ModelLoad.FindEntity");

                    foreach (SCAPI.ModelObject entity in Collect(
                        session,
                        root,
                        "Entity",
                        "ModelLoad.FindEntity",
                        stats))
                    {
                        if (!string.IsNullOrWhiteSpace(tableObjectId) &&
                            string.Equals(entity.ObjectId, tableObjectId, StringComparison.OrdinalIgnoreCase))
                        {
                            trace.SetResult("Found=True; FoundBy=FallbackObjectId");
                            return entity;
                        }

                        if (!string.IsNullOrWhiteSpace(tableName) &&
                            string.Equals(entity.Name, tableName, StringComparison.OrdinalIgnoreCase))
                        {
                            trace.SetResult("Found=True; FoundBy=Name");
                            return entity;
                        }
                    }
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    PerformanceTraceLogger.Error("ModelLoad.FindEntity", detail, ex);
                }

                trace.SetResult("Found=False");
                return null;
            }
        }

        private static bool IsAllowedClass(
            SCAPI.ModelObject scapiObject,
            string[] allowedClasses)
        {
            if (scapiObject == null || allowedClasses == null)
                return false;

            return allowedClasses.Contains(
                scapiObject.ClassName,
                StringComparer.OrdinalIgnoreCase);
        }

        private static ModelObject CreateModelObject(
            SCAPI.ModelObject scapiObject,
            ScapiLoadStats stats)
        {
            var stopwatch = Stopwatch.StartNew();
            var modelObject = new ModelObject();

            try
            {
                if (scapiObject == null)
                    return modelObject;

                modelObject.setoObjectId(SafeGetString(() => scapiObject.ObjectId));
                modelObject.setoClassName(SafeGetString(() => scapiObject.ClassName));
                modelObject.setoName(SafeGetString(() => scapiObject.Name));

                if (stats != null)
                    stats.ModelObjectsCreated++;

                return modelObject;
            }
            finally
            {
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds >= SlowPropertyPartThresholdMilliseconds)
                {
                    PerformanceTraceLogger.Info(
                        "ModelLoad.CreateModelObject.Slow",
                        FormatElapsed(stopwatch.Elapsed) + "; " + DescribeModelObject(modelObject));
                }
            }
        }

        private static List<ObjectProperty> ReadObjectProperties(
            SCAPI.ModelObject scapiObject,
            string caller,
            ScapiLoadStats stats)
        {
            var properties = new List<ObjectProperty>();
            string objectDetail =
                "Caller=" + CleanLogValue(caller) +
                "; " + DescribeModelObject(scapiObject);

            using (var trace = PerformanceTraceLogger.Start("ModelLoad.ReadObjectProperties", objectDetail))
            {
                if (scapiObject == null)
                {
                    trace.SetResult("Skipped=True; PropertyCount=0");
                    return properties;
                }

                int propertyErrors = 0;

                if (stats != null)
                    stats.ObjectsWithProperties++;

                try
                {
                    foreach (SCAPI.ModelProperty scapiProperty in scapiObject.Properties)
                    {
                        string propertyDetail =
                            objectDetail +
                            "; PropertyClassName=" + CleanLogValue(SafeGetString(() => scapiProperty.ClassName));
                        var propertyStopwatch = Stopwatch.StartNew();

                        try
                        {
                            var property = new ObjectProperty();
                            property.setoPropertyClassID(MeasureSlow(
                                "SCAPI.ModelProperty.ClassId",
                                propertyDetail,
                                () => scapiProperty.ClassId));
                            property.setoPropertyClassName(MeasureSlow(
                                "SCAPI.ModelProperty.ClassName",
                                propertyDetail,
                                () => scapiProperty.ClassName));
                            property.setoPropertyType(MeasureSlow(
                                "ModelLoad.PropertyDataType",
                                propertyDetail,
                                () => PropertyDataType(scapiProperty)));
                            property.setoPropertyValue(MeasureSlow(
                                "ModelLoad.RetrieveValue",
                                propertyDetail,
                                () => RetrieveValue(scapiProperty)));
                            property.setoPropertyFormatAsString(MeasureSlow(
                                "SCAPI.ModelProperty.FormatAsString",
                                propertyDetail,
                                () => scapiProperty.FormatAsString()));
                            properties.Add(property);

                            if (stats != null)
                                stats.PropertiesRead++;
                        }
                        catch (Exception ex)
                        {
                            propertyErrors++;

                            if (stats != null)
                                stats.PropertyReadErrors++;

                            PerformanceTraceLogger.Error(
                                "ModelLoad.ReadObjectProperties.PropertyError",
                                propertyDetail,
                                ex);
                        }
                        finally
                        {
                            propertyStopwatch.Stop();

                            if (propertyStopwatch.ElapsedMilliseconds >= SlowPropertyPartThresholdMilliseconds)
                            {
                                PerformanceTraceLogger.Info(
                                    "ModelLoad.ReadObjectProperties.PropertySlow",
                                    FormatElapsed(propertyStopwatch.Elapsed) + "; " + propertyDetail);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    PerformanceTraceLogger.Error("ModelLoad.ReadObjectProperties", objectDetail, ex);
                }

                trace.SetResult(
                    "PropertyCount=" + properties.Count +
                    "; PropertyErrors=" + propertyErrors);
                return properties;
            }
        }

        private static string ReadLocation(SCAPI.PersistenceUnit persistenceUnit)
        {
            using (var trace = PerformanceTraceLogger.Start(
                "ModelLoad.ReadLocation",
                DescribePersistenceUnit(persistenceUnit)))
            {
                try
                {
                    string location = persistenceUnit.PropertyBag["Locator"].Value["Locator"];
                    trace.SetResult("HasLocation=" + !string.IsNullOrWhiteSpace(location));
                    return location;
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    PerformanceTraceLogger.Error(
                        "ModelLoad.ReadLocation",
                        DescribePersistenceUnit(persistenceUnit),
                        ex);
                    return string.Empty;
                }
            }
        }

        private static SCAPI.Session OpenSession(
            SCAPI.Application application,
            SCAPI.PersistenceUnit persistenceUnit,
            string caller)
        {
            SCAPI.Session session = null;
            string detail =
                "Caller=" + CleanLogValue(caller) +
                "; " + DescribePersistenceUnit(persistenceUnit);

            using (var addTrace = PerformanceTraceLogger.Start("SCAPI.Sessions.Add", detail))
            {
                try
                {
                    session = application.Sessions.Add();
                    addTrace.SetResult("SessionCreated=True");
                }
                catch (Exception ex)
                {
                    addTrace.Fail(ex);
                    throw;
                }
            }

            using (var openTrace = PerformanceTraceLogger.Start("SCAPI.Session.Open", detail))
            {
                try
                {
                    session.Open(persistenceUnit, SCAPI.SC_SessionLevel.SCD_SL_M0);
                    openTrace.SetResult("Opened=True; Level=SCD_SL_M0");
                }
                catch (Exception ex)
                {
                    openTrace.Fail(ex);
                    throw;
                }
            }

            return session;
        }

        private static SCAPI.ModelObject ReadRoot(
            SCAPI.Session session,
            string caller)
        {
            using (var trace = PerformanceTraceLogger.Start(
                "SCAPI.ModelObjects.Root",
                "Caller=" + CleanLogValue(caller)))
            {
                try
                {
                    SCAPI.ModelObject root = session == null
                        ? null
                        : session.ModelObjects.Root;

                    trace.SetResult(DescribeModelObject(root));
                    return root;
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    throw;
                }
            }
        }

        private static void ReadModelHeader(
            ModelInfo model,
            SCAPI.PersistenceUnit persistenceUnit,
            SCAPI.ModelObject root,
            ModelLoadMode mode)
        {
            using (var trace = PerformanceTraceLogger.Start(
                "ModelLoad.ReadModelHeader",
                "Mode=" + mode + "; " + DescribePersistenceUnit(persistenceUnit)))
            {
                try
                {
                    model.setoName(SafeGetString(() => persistenceUnit.Name));
                    model.setoObjectId(SafeGetString(() => root.ObjectId));
                    model.setoLocation(ReadLocation(persistenceUnit));
                    trace.SetResult(
                        "ModelName=" + CleanLogValue(model.getoName()) +
                        "; RootObjectId=" + CleanLogValue(model.getoObjectId()));
                }
                catch (Exception ex)
                {
                    trace.Fail(ex);
                    throw;
                }
            }
        }

        private static void CloseSession(
            SCAPI.Application application,
            SCAPI.Session session,
            string caller)
        {
            string detail = "Caller=" + CleanLogValue(caller);

            if (session == null)
                return;

            using (var closeTrace = PerformanceTraceLogger.Start("SCAPI.Session.Close", detail))
            {
                try
                {
                    session.Close();
                    closeTrace.SetResult("Closed=True");
                }
                catch (Exception ex)
                {
                    closeTrace.Fail(ex);
                    PerformanceTraceLogger.Error("SCAPI.Session.Close", detail, ex);
                }
            }

            if (application == null)
                return;

            using (var removeTrace = PerformanceTraceLogger.Start("SCAPI.Sessions.Remove", detail))
            {
                try
                {
                    application.Sessions.Remove(session);
                    removeTrace.SetResult("Removed=True");
                }
                catch (Exception ex)
                {
                    removeTrace.Fail(ex);
                    PerformanceTraceLogger.Error("SCAPI.Sessions.Remove", detail, ex);
                }
            }
        }

        private static T MeasureSlow<T>(
            string operation,
            string detail,
            Func<T> action)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                return action == null ? default(T) : action();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PerformanceTraceLogger.Error(
                    operation,
                    FormatElapsed(stopwatch.Elapsed) + "; " + detail,
                    ex);
                throw;
            }
            finally
            {
                if (stopwatch.IsRunning)
                    stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds >= SlowPropertyPartThresholdMilliseconds)
                {
                    PerformanceTraceLogger.Info(
                        operation + ".Slow",
                        FormatElapsed(stopwatch.Elapsed) + "; " + detail);
                }
            }
        }

        private static string RetrieveValue(SCAPI.ModelProperty oProperty, int nIndex = -1)
        {
            try
            {
                bool isScalar = (oProperty.Flags & SCAPI.SC_ModelPropertyFlags.SCD_MPF_SCALAR) != 0;

                SCAPI.SC_ValueTypes valueType = isScalar
                    ? oProperty.DataType
                    : oProperty.DataType[nIndex];

                object value = isScalar
                    ? oProperty.Value
                    : oProperty.Value[nIndex];

                switch (valueType)
                {
                    case SCAPI.SC_ValueTypes.SCVT_I2:
                    case SCAPI.SC_ValueTypes.SCVT_I4:
                    case SCAPI.SC_ValueTypes.SCVT_UI1:
                    case SCAPI.SC_ValueTypes.SCVT_UI2:
                    case SCAPI.SC_ValueTypes.SCVT_UI4:
                    case SCAPI.SC_ValueTypes.SCVT_I1:
                    case SCAPI.SC_ValueTypes.SCVT_INT:
                    case SCAPI.SC_ValueTypes.SCVT_UINT:
                    case SCAPI.SC_ValueTypes.SCVT_I8:
                    case SCAPI.SC_ValueTypes.SCVT_UI8:
                    case SCAPI.SC_ValueTypes.SCVT_R4:
                    case SCAPI.SC_ValueTypes.SCVT_R8:
                    case SCAPI.SC_ValueTypes.SCVT_BOOLEAN:
                    case SCAPI.SC_ValueTypes.SCVT_CURRENCY:
                        return Convert.ToString(value);

                    case SCAPI.SC_ValueTypes.SCVT_DATE:
                        return Convert.ToDateTime(value).ToString("G");

                    case SCAPI.SC_ValueTypes.SCVT_BSTR:
                    case SCAPI.SC_ValueTypes.SCVT_GUID:
                    case SCAPI.SC_ValueTypes.SCVT_OBJID:
                        return value == null ? string.Empty : value.ToString();

                    case SCAPI.SC_ValueTypes.SCVT_BLOB:
                        return "<blob>";

                    case SCAPI.SC_ValueTypes.SCVT_RECT:
                        {
                            int[] array = (int[])value;
                            return string.Format("({0},{1},{2},{3})", array[0], array[1], array[2], array[3]);
                        }

                    case SCAPI.SC_ValueTypes.SCVT_POINT:
                        {
                            int[] array = (int[])value;
                            return string.Format("({0},{1})", array[0], array[1]);
                        }

                    case SCAPI.SC_ValueTypes.SCVT_SIZE:
                        {
                            int[] array = (int[])value;
                            return string.Format("{0}x{1}", array[0], array[1]);
                        }

                    default:
                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string PropertyDataType(SCAPI.ModelProperty oProperty)
        {
            try
            {
                string[] valueTypeNames =
                {
                    "Null", "I2", "I4", "UI1", "R4", "R8", "Bool", "$$", "IU", "ID",
                    "Date", "Str", "UI2", "UI4", "Guid", "Id", "Blob", "Def", "I1",
                    "IT", "UIT", "Rect", "Pnt", "I8", "UI8", "Size"
                };

                bool isScalar = (oProperty.Flags & SCAPI.SC_ModelPropertyFlags.SCD_MPF_SCALAR) != 0;
                SCAPI.SC_ValueTypes valueType = isScalar
                    ? oProperty.DataType
                    : oProperty.DataType[0];

                int typeIndex = (int)valueType;
                return typeIndex >= 0 && typeIndex < valueTypeNames.Length
                    ? valueTypeNames[typeIndex]
                    : "Unknown (" + typeIndex + ")";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string DescribePersistenceUnit(SCAPI.PersistenceUnit persistenceUnit)
        {
            if (persistenceUnit == null)
                return "PersistenceUnit=<null>";

            return "PersistenceUnitName=" + CleanLogValue(SafeGetString(() => persistenceUnit.Name)) +
                   "; PersistenceUnitObjectId=" + CleanLogValue(SafeGetString(() => persistenceUnit.ObjectId));
        }

        private static string DescribeModelObject(SCAPI.ModelObject modelObject)
        {
            if (modelObject == null)
                return "ModelObject=<null>";

            return "ObjectClass=" + CleanLogValue(SafeGetString(() => modelObject.ClassName)) +
                   "; ObjectName=" + CleanLogValue(SafeGetString(() => modelObject.Name)) +
                   "; ObjectId=" + CleanLogValue(SafeGetString(() => modelObject.ObjectId));
        }

        private static string DescribeModelObject(ModelObject modelObject)
        {
            if (modelObject == null)
                return "ModelObject=<null>";

            return "ObjectClass=" + CleanLogValue(modelObject.getoClassName()) +
                   "; ObjectName=" + CleanLogValue(modelObject.getoName()) +
                   "; ObjectId=" + CleanLogValue(modelObject.getoObjectId());
        }

        private static string FormatAllowedClasses(string[] allowedClasses)
        {
            return allowedClasses == null
                ? "<null>"
                : string.Join(",", allowedClasses);
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return "ElapsedMs=" + elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string SafeGetString(Func<string> getter)
        {
            try
            {
                return getter == null
                    ? string.Empty
                    : getter() ?? string.Empty;
            }
            catch (Exception ex)
            {
                return "<error: " + CleanLogValue(ex.Message) + ">";
            }
        }

        private static string CleanLogValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return PerformanceTraceLogger.Truncate(
                value.Replace("\r", " ").Replace("\n", " "),
                500);
        }

        private sealed class ScapiLoadStats
        {
            private readonly string loadKind;

            public ScapiLoadStats(string loadKind)
            {
                this.loadKind = loadKind;
            }

            public int ChildLoadCalls { get; set; }

            public int CollectCalls { get; set; }

            public int CollectErrors { get; set; }

            public int CollectedObjects { get; set; }

            public int ModelObjectsCreated { get; set; }

            public int ObjectsWithProperties { get; set; }

            public int PropertiesRead { get; set; }

            public int PropertyReadErrors { get; set; }

            public string ToSummary()
            {
                return "LoadKind=" + loadKind +
                       "; ChildLoadCalls=" + ChildLoadCalls +
                       "; CollectCalls=" + CollectCalls +
                       "; CollectErrors=" + CollectErrors +
                       "; CollectedObjects=" + CollectedObjects +
                       "; ModelObjectsCreated=" + ModelObjectsCreated +
                       "; ObjectsWithProperties=" + ObjectsWithProperties +
                       "; PropertiesRead=" + PropertiesRead +
                       "; PropertyReadErrors=" + PropertyReadErrors;
            }
        }

        private enum ModelLoadMode
        {
            Summary,
            TableUdpsOnly,
            Full
        }
    }
}
