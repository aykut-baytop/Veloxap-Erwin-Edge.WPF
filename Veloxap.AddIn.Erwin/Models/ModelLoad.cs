using System;
using System.Collections.Generic;
using System.Linq;
using Veloxap.AddIn.Erwin;

namespace Veloxap.AddIn.Erwin.Models
{
    internal class ModelLoad
    {
        private static readonly string[] DefaultObjectClasses =
        {
            "Entity",
            "Relationship",
            "Attribute",
            "Sequence",
            "Key_Group",
            "Key_Group_Member"
        };

        private static readonly HashSet<string> TableUdpPropertyNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "erisilebilirlik",
                "butunluk",
                "gizlilikseviyesi",
                "veridegeri",
                "issureciseviyesi",
                "bankagorecedegeri",
                "kisiselverimi",
                "hassasverimi",
                "guvenliksinifidegeri"
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
            oApplication = ScapiApplicationProvider.GetApplication();
        }

        public List<ModelObject> loadTableSummaries(SCAPI.PersistenceUnit oPersistenceUnit)
        {
            var tables = new List<ModelObject>();

            if (oPersistenceUnit == null || oApplication == null)
                return tables;

            SCAPI.Session session = null;

            try
            {
                session = oApplication.Sessions.Add();
                session.Open(oPersistenceUnit, SCAPI.SC_SessionLevel.SCD_SL_M0);

                foreach (SCAPI.ModelObject entity in Collect(session, session.ModelObjects.Root, "Entity"))
                    tables.Add(CreateModelObject(entity));
            }
            catch
            {
            }
            finally
            {
                CloseSession(oApplication, session);
            }

            return tables;
        }

        public ModelObject loadTableUdpObject(
            SCAPI.PersistenceUnit oPersistenceUnit,
            string tableObjectId,
            string tableName)
        {
            if (oPersistenceUnit == null || oApplication == null)
                return null;

            SCAPI.Session session = null;

            try
            {
                session = oApplication.Sessions.Add();
                session.Open(oPersistenceUnit, SCAPI.SC_SessionLevel.SCD_SL_M0);

                SCAPI.ModelObject entity = FindEntity(
                    session,
                    tableObjectId,
                    tableName);

                if (entity == null)
                    return null;

                ModelObject table = CreateModelObject(entity);
                table.setoObjectProperty(ReadObjectProperties(entity, IsTableUdpProperty));
                table.setoModelObjects(LoadChildObjects(
                    session,
                    entity,
                    new[] { "Attribute" },
                    1,
                    IsTableUdpProperty));

                return table;
            }
            catch
            {
                return null;
            }
            finally
            {
                CloseSession(oApplication, session);
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

            if (oPersistenceUnit == null || oApplication == null)
                return model;

            SCAPI.Session session = null;

            try
            {
                session = oApplication.Sessions.Add();
                session.Open(oPersistenceUnit, SCAPI.SC_SessionLevel.SCD_SL_M0);

                SCAPI.ModelObject root = session.ModelObjects.Root;
                model.setoName(oPersistenceUnit.Name);
                model.setoObjectId(root.ObjectId);
                model.setoLocation(ReadLocation(oPersistenceUnit));
                model.setoObjectProperty(ReadObjectProperties(root));

                if (mode == ModelLoadMode.Summary)
                    return model;

                model.setoModelObject(
                    mode == ModelLoadMode.TableUdpsOnly
                        ? LoadTableUdpObjects(session, root)
                        : LoadChildObjects(session, root, DefaultObjectClasses, 2));

                return model;
            }
            catch
            {
                return model;
            }
            finally
            {
                CloseSession(oApplication, session);
            }
        }

        private static List<ModelObject> LoadTableUdpObjects(
            SCAPI.Session session,
            SCAPI.ModelObject root)
        {
            var tables = new List<ModelObject>();

            foreach (SCAPI.ModelObject entity in Collect(session, root, "Entity"))
            {
                ModelObject table = CreateModelObject(entity);
                table.setoObjectProperty(ReadObjectProperties(entity, IsTableUdpProperty));
                table.setoModelObjects(LoadChildObjects(
                    session,
                    entity,
                    new[] { "Attribute" },
                    1,
                    IsTableUdpProperty));
                tables.Add(table);
            }

            return tables;
        }

        private static List<ModelObject> LoadChildObjects(
            SCAPI.Session session,
            SCAPI.ModelObject parent,
            string[] allowedClasses,
            int remainingDepth,
            Predicate<string> propertyFilter = null)
        {
            var modelObjects = new List<ModelObject>();

            if (session == null || parent == null || remainingDepth <= 0)
                return modelObjects;

            foreach (SCAPI.ModelObject scapiObject in Collect(session, parent, null))
            {
                if (!IsAllowedClass(scapiObject, allowedClasses))
                    continue;

                ModelObject modelObject = CreateModelObject(scapiObject);
                modelObject.setoObjectProperty(ReadObjectProperties(scapiObject, propertyFilter));
                modelObject.setoModelObjects(LoadChildObjects(
                    session,
                    scapiObject,
                    allowedClasses,
                    remainingDepth - 1,
                    propertyFilter));
                modelObjects.Add(modelObject);
            }

            return modelObjects;
        }

        private static List<SCAPI.ModelObject> Collect(
            SCAPI.Session session,
            SCAPI.ModelObject parent,
            string className)
        {
            var objects = new List<SCAPI.ModelObject>();

            if (session == null || parent == null)
                return objects;

            try
            {
                SCAPI.ModelObjects selectedCollection = session.ModelObjects.Collect(
                    parent.ObjectId,
                    className,
                    1);

                foreach (SCAPI.ModelObject scapiObject in selectedCollection)
                    objects.Add(scapiObject);
            }
            catch
            {
            }

            return objects;
        }

        private static SCAPI.ModelObject FindEntity(
            SCAPI.Session session,
            string tableObjectId,
            string tableName)
        {
            if (session == null)
                return null;

            if (!string.IsNullOrWhiteSpace(tableObjectId))
            {
                try
                {
                    return session.ModelObjects[tableObjectId];
                }
                catch
                {
                }
            }

            try
            {
                foreach (SCAPI.ModelObject entity in Collect(session, session.ModelObjects.Root, "Entity"))
                {
                    if (!string.IsNullOrWhiteSpace(tableObjectId) &&
                        string.Equals(entity.ObjectId, tableObjectId, StringComparison.OrdinalIgnoreCase))
                    {
                        return entity;
                    }

                    if (!string.IsNullOrWhiteSpace(tableName) &&
                        string.Equals(entity.Name, tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entity;
                    }
                }
            }
            catch
            {
            }

            return null;
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

        private static ModelObject CreateModelObject(SCAPI.ModelObject scapiObject)
        {
            var modelObject = new ModelObject();

            if (scapiObject == null)
                return modelObject;

            modelObject.setoObjectId(scapiObject.ObjectId);
            modelObject.setoClassName(scapiObject.ClassName);
            modelObject.setoName(scapiObject.Name);

            return modelObject;
        }

        private static List<ObjectProperty> ReadObjectProperties(SCAPI.ModelObject scapiObject)
        {
            return ReadObjectProperties(scapiObject, null);
        }

        private static List<ObjectProperty> ReadObjectProperties(
            SCAPI.ModelObject scapiObject,
            Predicate<string> propertyFilter)
        {
            var properties = new List<ObjectProperty>();

            if (scapiObject == null)
                return properties;

            try
            {
                foreach (SCAPI.ModelProperty scapiProperty in scapiObject.Properties)
                {
                    try
                    {
                        string propertyClassName = GetPropertyClassName(scapiProperty);
                        if (propertyFilter != null && !propertyFilter(propertyClassName))
                            continue;

                        var property = new ObjectProperty();
                        property.setoPropertyClassID(scapiProperty.ClassId);
                        property.setoPropertyClassName(propertyClassName);
                        property.setoPropertyType(PropertyDataType(scapiProperty));
                        property.setoPropertyValue(RetrieveValue(scapiProperty));
                        property.setoPropertyFormatAsString(scapiProperty.FormatAsString());
                        properties.Add(property);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return properties;
        }

        private static string GetPropertyClassName(SCAPI.ModelProperty property)
        {
            try
            {
                return property == null || property.ClassName == null
                    ? string.Empty
                    : property.ClassName.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsTableUdpProperty(string propertyName)
        {
            string normalizedName = NormalizePropertyName(propertyName);
            if (string.IsNullOrWhiteSpace(normalizedName))
                return false;

            if (normalizedName.EndsWith(
                "sirkapsamindakiveridegeri",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (string tableUdpPropertyName in TableUdpPropertyNames)
            {
                if (normalizedName.EndsWith(
                    tableUdpPropertyName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePropertyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            int dotIndex = value.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < value.Length - 1)
                value = value.Substring(dotIndex + 1);

            var builder = new System.Text.StringBuilder(value.Length);
            foreach (char valueChar in value)
            {
                char c = FoldTurkishCharacter(char.ToLowerInvariant(valueChar));
                if (char.IsLetterOrDigit(c))
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static char FoldTurkishCharacter(char c)
        {
            switch (c)
            {
                case '\u0130':
                case '\u0131':
                    return 'i';
                case '\u011e':
                case '\u011f':
                    return 'g';
                case '\u015e':
                case '\u015f':
                    return 's';
                case '\u00c7':
                case '\u00e7':
                    return 'c';
                case '\u00d6':
                case '\u00f6':
                    return 'o';
                case '\u00dc':
                case '\u00fc':
                    return 'u';
                default:
                    return c;
            }
        }

        private static string ReadLocation(SCAPI.PersistenceUnit persistenceUnit)
        {
            try
            {
                return persistenceUnit.PropertyBag["Locator"].Value["Locator"];
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void CloseSession(
            SCAPI.Application application,
            SCAPI.Session session)
        {
            if (session == null)
                return;

            try
            {
                session.Close();
            }
            catch
            {
            }

            if (application == null)
                return;

            try
            {
                application.Sessions.Remove(session);
            }
            catch
            {
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

        private enum ModelLoadMode
        {
            Summary,
            TableUdpsOnly,
            Full
        }
    }
}
