using SCAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Services;
using static System.Net.Mime.MediaTypeNames;

namespace VeloxapEDGErwinTools.AddIn
{
    internal class VeloxapEDGErwinLib
    {
        private SCAPI.Application oApplication;



        public VeloxapEDGErwinLib(ref SCAPI.Application oApp)
        {
            oApplication = oApp;


        }

        public SCAPI.PersistenceUnit getPersistenceUnit(int modelIndex)
        {
            if (oApplication == null || oApplication.PersistenceUnits == null)
                return null;

            if (modelIndex < 0 || modelIndex >= oApplication.PersistenceUnits.Count)
                return null;

            return oApplication.PersistenceUnits[modelIndex];
        }

        public ModelInfo loadModelSummary(string objectId, string pobjectId)
        {
            SCAPI.PersistenceUnit oPersistenceUnit = findPersistenceUnit(pobjectId);
            if (oPersistenceUnit == null)
                return new ModelInfo();

            ModelLoad mLoad = new ModelLoad(ref oApplication);
            return mLoad.loadModelSummary(oPersistenceUnit);
        }

        public ModelInfo loadTableUdpModelObject(string objectId, string pobjectId)
        {
            SCAPI.PersistenceUnit oPersistenceUnit = findPersistenceUnit(pobjectId);
            if (oPersistenceUnit == null)
                return new ModelInfo();

            ModelLoad mLoad = new ModelLoad(ref oApplication);
            return mLoad.loadTableUdpModel(oPersistenceUnit);
        }

        public List<(string, string, string)> getModelsNamePath()
        {

            if (oApplication == null) return null;

            List<(string value, string key1, string key2)> oModelsName = new List<(string, string, string)>();

            SCAPI.PropertyBag oBag;
            SCAPI.Session oSession;
            SCAPI.ModelObject oRoot;
            String sTitle;
            String sLocation;
            String sObjectId;
            String pObjectId;

            foreach (PersistenceUnit oUnit in oApplication.PersistenceUnits)
            {


                oSession = oApplication.Sessions.Add();
                oSession.Open(oUnit, SCAPI.SC_SessionLevel.SCD_SL_M0);
                oRoot = oSession.ModelObjects.Root;
                pObjectId = oUnit.ObjectId;
                sTitle = oRoot.Name;
                sObjectId = oRoot.ObjectId;
                oBag = oUnit.PropertyBag["Locator;Hidden_Model"];

                try
                {
                    sLocation = oBag.Value["Locator"]; //Get the location
                    if (sLocation.Length > 0)
                        sTitle = sTitle + " (" + sLocation + ")";

                    if (oBag.Value["Hidden_Model"])
                        sTitle = sTitle + " [Hidden]"; //Check if the persistence unit is hidden

                    oBag.ClearAll();
                }
                catch (Exception e)
                {
                    oApplication.Sessions.Clear();
                    //MessageBox.Show(e.ToString());
                    // uyari

                    return null;
                }
                oApplication.Sessions.Clear();

                oModelsName.Add((sTitle, sObjectId, pObjectId));
            }

            return oModelsName;

        }
        public ModelInfo loadModelObject(string objectId, string pobjectId)
        {
            ModelInfo mModel = new ModelInfo();


            SCAPI.ModelObjects oSelectedCollection;
            SCAPI.Session oSession;

            SCAPI.PersistenceUnit oPersistenceUnit;
            SCAPI.PersistenceUnits oPersistenceUnits;
            SCAPI.SC_SessionLevel eLevel;


            oPersistenceUnits = oApplication.PersistenceUnits;
            int index = -1;
            bool isPersistenceUnitFound = false;
            foreach (SCAPI.PersistenceUnit oUnit in oPersistenceUnits)
            {
                index++;
                if (oUnit.ObjectId == pobjectId)
                {
                    isPersistenceUnitFound = true;
                    break;
                }


            }
            if (isPersistenceUnitFound && index >= 0)
            {

                //
                /*
                 *  Filtrelerde kullanilan properitiesleri listeye ekleyerek sadece 
                 *  onlari yukleyecegiz.
                 *  
                 *  
                 *  
                 */
                //   eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
                //  oSession = oApplication.Sessions.Add();

                oPersistenceUnit = oApplication.PersistenceUnits[index]; // combo box level
                ModelLoad mLoad = new ModelLoad(ref oApplication);

                mModel = mLoad.loadModel(oPersistenceUnit);


                // oSession.Open(oPersistenceUnit, eLevel);
                /*
                 var objectlist = new[] { "Entity", "Relationship", "Attribute","Sequence", "Key_Group", "Key_Group_Member" };
                 oSelectedCollection = oSession.ModelObjects.Collect(objectId, null, 1);

                 // Model genel bilgileri
                 mModel.setoName(oPersistenceUnit.Name);
                 mModel.setoObjectId(oSession.ModelObjects.Root.ObjectId);
                 mModel.setoLocation(oPersistenceUnit.PropertyBag["Locator"].Value["Locator"]);


                 // Model Object Property
                 List<ObjectProperty> mObjectProperties = loadObjectProperities(true, oSession.ModelObjects.Root.ObjectId, null, oPersistenceUnit);
                 mModel.setoObjectProperty(mObjectProperties);


                 List<ModelObject > mModelObjects = new List<ModelObject>();


                 foreach (SCAPI.ModelObject oObject in oSelectedCollection)
                 {
                     ModelObject mModelObject = new ModelObject();

                     if (objectlist.Contains(oObject.ClassName)  )
                     {
                         mModelObject.setoObjectId(oObject.ObjectId);
                         mModelObject.setoClassName(oObject.ClassName);
                         mModelObject.setoName(oObject.Name);


                         // Model Object main properities (etc: D_Arac ve D_Arac ozellikleri)
                         List<ObjectProperty> mObjectProperty = loadObjectProperities(false, oObject.ObjectId, oSession.ModelObjects.Root.ObjectId, oPersistenceUnit);


                         // D_Arac tablosunun sutunlari ve sutunlarin ozellikleri
                         mModelObject.setoModelObjects(loadSubModelObject(oObject.ObjectId, oPersistenceUnit));



                         mModelObject.setoObjectProperty(mObjectProperty);


                         mModelObjects.Add(mModelObject);



                     }


                 }

                 // Model Object
                 mModel.setoModelObject(mModelObjects);

                 oApplication.Sessions.Clear();
                */
            }
            return mModel;
        }

        private SCAPI.PersistenceUnit findPersistenceUnit(string pobjectId)
        {
            if (oApplication == null || oApplication.PersistenceUnits == null)
                return null;

            foreach (SCAPI.PersistenceUnit oUnit in oApplication.PersistenceUnits)
            {
                if (oUnit.ObjectId == pobjectId)
                    return oUnit;
            }

            return null;
        }
        public List<Veloxap.AddIn.Erwin.Models.ModelObject> loadSubModelObject(string objectId, SCAPI.PersistenceUnit oPersistenceUnit)
        {
            List<Veloxap.AddIn.Erwin.Models.ModelObject> mModelObjects = new List<Veloxap.AddIn.Erwin.Models.ModelObject>();


            SCAPI.ModelObjects oSelectedCollection;
            SCAPI.Session oSession;

            SCAPI.SC_SessionLevel eLevel;



            eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
            oSession = oApplication.Sessions.Add();

            oSession.Open(oPersistenceUnit, eLevel);

            var objectlist = new[] { "Entity", "Relationship", "Attribute", "Sequence", "Key_Group", "Key_Group_Member" };
            oSelectedCollection = oSession.ModelObjects.Collect(objectId, null, 1);




            foreach (SCAPI.ModelObject oObject in oSelectedCollection)
            {
                Veloxap.AddIn.Erwin.Models.ModelObject mModelObject = new Veloxap.AddIn.Erwin.Models.ModelObject();

                if (objectlist.Contains(oObject.ClassName))
                {
                    mModelObject.setoObjectId(oObject.ObjectId);
                    mModelObject.setoClassName(oObject.ClassName);
                    mModelObject.setoName(oObject.Name);


                    List<ObjectProperty> mObjectProperty = loadObjectProperities(false, oObject.ObjectId, oSession.ModelObjects.Root.ObjectId, oPersistenceUnit);
                    mModelObject.setoObjectProperty(mObjectProperty);
                    mModelObjects.Add(mModelObject);



                }


            }


            return mModelObjects;
        }
        public List<ObjectProperty> loadObjectProperities(bool isRoot, object objectId, object parentObjectId, SCAPI.PersistenceUnit oPersistenceUnit)
        {

            List<ObjectProperty> mObjectProperties = new List<ObjectProperty>();

            SCAPI.Session oSession;
            SCAPI.ModelObject oRootObject;
            SCAPI.ModelObject oObject;

            SCAPI.SC_SessionLevel eLevel;

            eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
            oSession = oApplication.Sessions.Add();

            try
            {
                oSession.Open(oPersistenceUnit, eLevel);


                if (isRoot)
                    oRootObject = oSession.ModelObjects.Root;
                else
                    oRootObject = oSession.ModelObjects[parentObjectId];



                oObject = oSession.ModelObjects.Collect(oRootObject)[objectId];


                if (oObject != null)
                {
                    foreach (SCAPI.ModelProperty oProperty in oObject.Properties)
                    {
                        ObjectProperty mObjectProperty = new ObjectProperty();
                        try
                        {

                            string type = PropertyDataType(oProperty);
                            string format = oProperty.FormatAsString();
                            string val = RetrieveValue(oProperty);


                            mObjectProperty.setoPropertyClassID(oProperty.ClassId);
                            mObjectProperty.setoPropertyClassName(oProperty.ClassName);
                            mObjectProperty.setoPropertyType(type);
                            mObjectProperty.setoPropertyValue(val);
                            mObjectProperty.setoPropertyFormatAsString(format);

                            mObjectProperties.Add(mObjectProperty);


                        }
                        catch (Exception e) { }


                    }



                }
            }
            catch (Exception e)
            {

            }
            return mObjectProperties;
        }

        public List<(string, string, string)> getModelObjects(string objectId, int selectedModelIndex)
        {
            //MessageBox.Show("getModelObjects");
            List<(string, string, string)> modelObjectsList = new List<(string, string, string)>();

            SCAPI.ModelObjects oSelectedCollection;
            SCAPI.Session oSession;

            SCAPI.PersistenceUnit oPersistenceUnit;
            SCAPI.SC_SessionLevel eLevel;



            eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
            oSession = oApplication.Sessions.Add();

            oPersistenceUnit = oApplication.PersistenceUnits[selectedModelIndex]; // combo box level

            oSession.Open(oPersistenceUnit, eLevel);

            var objectlist = new[] { "Entity" };
            oSelectedCollection = oSession.ModelObjects.Collect(objectId, null, 1);
            //modelObjectsList.Add(("Model", oPersistenceUnit.Name, oPersistenceUnit.ObjectId));

            foreach (SCAPI.ModelObject oObject in oSelectedCollection)
            {
                if (objectlist.Contains(oObject.ClassName))
                    modelObjectsList.Add((oObject.ClassName, oObject.Name, oObject.ObjectId));


            }
            oApplication.Sessions.Clear();

            //MessageBox.Show("getModelObjects END");

            //if (modelObjectsList != null && modelObjectsList.Count > 0)
            //{
            //    var list = modelObjectsList.Select(x => $"{x.Item1}|{x.Item2}|{x.Item3}");
            //    ScapiTraceLogger.Info(Environment.NewLine + "getModelObjects" + string.Join(",", list) + Environment.NewLine);
            //}

            return modelObjectsList;
        }

        private string RetrieveValue(SCAPI.ModelProperty oProperty, int nIndex = -1)
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
                        return value?.ToString() ?? string.Empty;

                    case SCAPI.SC_ValueTypes.SCVT_BLOB:
                        return "<blob>";

                    case SCAPI.SC_ValueTypes.SCVT_RECT:
                        {
                            int[] array = (int[])value;
                            return $"({array[0]},{array[1]},{array[2]},{array[3]})";
                        }

                    case SCAPI.SC_ValueTypes.SCVT_POINT:
                        {
                            int[] array = (int[])value;
                            return $"({array[0]},{array[1]})";
                        }

                    case SCAPI.SC_ValueTypes.SCVT_SIZE:
                        {
                            int[] array = (int[])value;
                            return $"{array[0]}x{array[1]}";
                        }

                    default:
                        return "";// $"<error: variant type - {value?.GetType().Name ?? "<null>"} SCAPI type - {(int)valueType}>";
                }
            }
            catch (Exception ex)
            {
                string className;
                try
                {
                    className = oProperty.ClassName;
                }
                catch
                {
                    className = "<unknown>";
                }

                return "";//$"Failed to populate property {className} with error {ex.Message}";
            }
        }

        private string PropertyDataType(SCAPI.ModelProperty oProperty)
        {
            string dataType = "";
            try
            {
                var flags = oProperty.Flags;


                string[] valueTypeNames =
                {
            "Null","I2","I4","UI1","R4","R8","Bool","$$","IU","ID",
            "Date","Str","UI2","UI4","Guid","Id","Blob","Def","I1",
            "IT","UIT","Rect","Pnt","I8","UI8","Size"
        };

                bool isScalar = (flags & SCAPI.SC_ModelPropertyFlags.SCD_MPF_SCALAR) != 0;

                SCAPI.SC_ValueTypes valueType = isScalar
                    ? oProperty.DataType
                    : oProperty.DataType[0];

                int typeIndex = (int)valueType;

                if (typeIndex >= 0 && typeIndex < valueTypeNames.Length)
                    dataType = valueTypeNames[typeIndex];
                else
                    dataType = $"Unknown ({typeIndex})";



            }
            catch (Exception ex)
            {
                string className;
                try
                {
                    className = oProperty.ClassName;
                }
                catch
                {
                    className = "<unknown>";
                }

                //return  $"Failed to collect flags for a property of {className} class with error {ex.Message}";
            }
            return dataType;
        }

        #region Eski GetObjectProperties
        //public List<(string, string, string, string)> getObjectProperities(bool isRoot, object objectId, object parentObjectId, int selectedModelIndex)
        //{
        //    //MessageBox.Show("getObjectProperities");

        //    List<(string, string, string, string)> objectProperities = new List<(string, string, string, string)>();
        //    SCAPI.Session oSession;
        //    SCAPI.ModelObject oRootObject;
        //    SCAPI.ModelObject oObject;

        //    SCAPI.PersistenceUnit oPersistenceUnit;
        //    SCAPI.SC_SessionLevel eLevel;

        //    eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
        //    oSession = oApplication.Sessions.Add();

        //    try
        //    {
        //        oPersistenceUnit = oApplication.PersistenceUnits[selectedModelIndex];
        //        oSession.Open(oPersistenceUnit, eLevel);

        //        if (isRoot)
        //            oRootObject = oSession.ModelObjects.Root;
        //        else
        //            oRootObject = oSession.ModelObjects[parentObjectId];

        //        oObject = oSession.ModelObjects.Collect(oRootObject)[objectId];
        //        //oObject.
        //        if (oObject != null)
        //        {
        //            var aktiftechTableNodes = new HashSet<string>(
        //                    StringComparer.OrdinalIgnoreCase)
        //                {
        //                    "Entity.Physical.Veri_Degeri",
        //                    "Entity.Physical.Banka_Gorece_Degeri",
        //                    "Entity.Physical.Guvenlik_Sinifi_Degeri",
        //                    "Entity.Physical.Hassas_Veri_Mi",
        //                    "Entity.Physical.Kisisel_Veri_Mi",
        //                    "Entity.Physical.Erisilebilirlik",
        //                    "Entity.Physical.Butunluk",
        //                    "Entity.Physical.Gizlilik_Seviyesi",
        //                    "Entity.Physical.Is_Sureci_Seviyesi"
        //                };

        //            var subColumns = new List<string>();

        //            foreach (SCAPI.ModelProperty oProperty in oObject.Properties)
        //            {
        //                if (aktiftechTableNodes.Contains(oProperty.ClassName))
        //                {
        //                    try
        //                    {
        //                        string type = PropertyDataType(oProperty);
        //                        string format = oProperty.FormatAsString();
        //                        string val = RetrieveValue(oProperty);
        //                        //MessageBox.Show($"className : {oProperty.ClassName} type : {type}  / format : {format} / value : {val}");

        //                        objectProperities.Add((oProperty.ClassName, type, format, val));
        //                    }
        //                    catch (Exception e) { }
        //                }

        //                if (oProperty.ClassName == "Attributes_Order_Ref")
        //                {
        //                    foreach (var item in oProperty.FormatAsString().Split(';'))
        //                    {
        //                        subColumns.Add(item);
        //                    }
        //                    //MessageBox.Show($"Attributes_Order_Ref className : {oProperty.ClassName} type : {PropertyDataType(oProperty)}  / format : {oProperty.FormatAsString()} / value : {RetrieveValue(oProperty)}");
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {

        //    }
        //    oApplication.Sessions.Clear();
        //    //MessageBox.Show("getObjectProperities END");

        //    if (objectProperities != null && objectProperities.Count > 0)
        //    {
        //        var list = objectProperities.Select(x => $"{x.Item1}|{x.Item2}|{x.Item3}|{x.Item4}" + Environment.NewLine);
        //        ScapiTraceLogger.Info(Environment.NewLine + "getObjectProperities" + string.Join(",", list) + Environment.NewLine);
        //        ScapiTraceLogger.Info(Environment.NewLine + "subColumns" + string.Join(",", list) + Environment.NewLine);
        //    }

        //    return objectProperities;
        //}

        #endregion
        public ObjectPropertiesResult GetObjectProperties(
            bool isRoot,
            object objectId,
            object parentObjectId,
            int selectedModelIndex)
        {
            var result = new ObjectPropertiesResult();

            SCAPI.Session session = null;

            try
            {
                SCAPI.PersistenceUnit persistenceUnit =
                    oApplication.PersistenceUnits[selectedModelIndex];

                session = oApplication.Sessions.Add();
                session.Open(
                    persistenceUnit,
                    SCAPI.SC_SessionLevel.SCD_SL_M0);
                return GetObjectPropertiesInOpenSession(
                    session,
                    isRoot,
                    objectId,
                    parentObjectId);
            }
            catch (Exception ex)
            {
                ScapiTraceLogger.Info(
                    Environment.NewLine +
                    "GetObjectProperties ERROR" +
                    Environment.NewLine +
                    ex +
                    Environment.NewLine);

                return result;
            }
            finally
            {
                try
                {
                    oApplication.Sessions.Clear();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Reads the Model & Tablo Bilgileri details for a model in one SCAPI
        /// session. The isolated UI host needs these serializable details, and
        /// opening a separate session for every entity is prohibitively slow for
        /// large models.
        /// </summary>
        public Dictionary<string, ObjectPropertiesResult> GetObjectPropertiesBatch(
            string rootObjectId,
            IEnumerable<string> objectIds,
            int selectedModelIndex)
        {
            var results = new Dictionary<string, ObjectPropertiesResult>(
                StringComparer.OrdinalIgnoreCase);
            SCAPI.Session session = null;

            try
            {
                SCAPI.PersistenceUnit persistenceUnit =
                    oApplication.PersistenceUnits[selectedModelIndex];
                session = oApplication.Sessions.Add();
                session.Open(persistenceUnit, SCAPI.SC_SessionLevel.SCD_SL_M0);

                foreach (string objectId in objectIds ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(objectId))
                        continue;

                    results[objectId] = GetObjectPropertiesInOpenSession(
                        session,
                        false,
                        objectId,
                        rootObjectId);
                }

                return results;
            }
            catch (Exception ex)
            {
                ScapiTraceLogger.Info(
                    Environment.NewLine +
                    "GetObjectPropertiesBatch ERROR" +
                    Environment.NewLine +
                    ex +
                    Environment.NewLine);
                return results;
            }
            finally
            {
                try
                {
                    oApplication.Sessions.Clear();
                }
                catch
                {
                }
            }
        }

        private ObjectPropertiesResult GetObjectPropertiesInOpenSession(
            SCAPI.Session session,
            bool isRoot,
            object objectId,
            object parentObjectId)
        {
            var result = new ObjectPropertiesResult();
            if (session == null)
                return result;

            SCAPI.ModelObject selectedObject;

            // Object ids are globally addressable within an open model session.
            // Avoid Collect(root) for every entity: on a large model that would
            // repeatedly materialize the entire entity collection.
            try
            {
                selectedObject = isRoot
                    ? session.ModelObjects.Root
                    : session.ModelObjects[objectId];
            }
            catch
            {
                SCAPI.ModelObject parentObject = isRoot
                    ? session.ModelObjects.Root
                    : session.ModelObjects[parentObjectId];
                selectedObject = session.ModelObjects.Collect(parentObject)[objectId];
            }

            if (selectedObject == null || !IsEntityObject(selectedObject))
                return result;

            var entityPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Entity.Physical.Veri_Degeri",
                "Entity.Physical.Banka_Gorece_Degeri",
                "Entity.Physical.Guvenlik_Sinifi_Degeri",
                "Entity.Physical.Hassas_Veri_Mi",
                "Entity.Physical.Erisilebilirlik",
                "Entity.Physical.Butunluk",
                "Entity.Physical.Bütünlük",
                "Entity.Physical.Gizlilik_Seviyesi",
                "Entity.Physical.Is_Sureci_Seviyesi",
                "Entity.Physical.Sir_Kapsaminda_Veri_Mi"
            };
            var attributePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Attribute.Physical.Hassas_Veri_Mi",
                "Attribute.Physical.Kisisel_Veri_Mi"
            };

            ReadProperties(selectedObject, entityPropertyNames, result.EntityProperties);

            foreach (SCAPI.ModelObject attribute in session.ModelObjects.Collect(selectedObject, "Attribute"))
            {
                if (attribute == null)
                    continue;

                var columnInfo = new ScapiColumnInfo
                {
                    ObjectId = Convert.ToString(attribute.ObjectId),
                    Name = GetObjectName(attribute)
                };
                ReadProperties(attribute, attributePropertyNames, columnInfo.Properties);
                result.Columns.Add(columnInfo);
            }

            return result;
        }

        private void ReadProperties(
    SCAPI.ModelObject modelObject,
    HashSet<string> targetPropertyNames,
    List<ScapiPropertyInfo> targetList)
        {
            foreach (SCAPI.ModelProperty property in modelObject.Properties)
            {
                if (!targetPropertyNames.Contains(property.ClassName))
                    continue;

                try
                {

                    targetList.Add(new ScapiPropertyInfo
                    {
                        ClassName = property.ClassName,
                        DataType = PropertyDataType(property),
                        Format = SafeFormatAsString(property),
                        Value = RetrieveValue(property)
                    });

                    ScapiTraceLogger.Info(modelObject.ClassName + " - property : " + property.ClassName + " // " + PropertyDataType(property) + " - " + SafeFormatAsString(property) + " - " + RetrieveValue(property) +
                        Environment.NewLine);
                }
                catch (Exception ex)
                {
                    ScapiTraceLogger.Info(
                        "Property okunamadı: " +
                        property.ClassName +
                        " / " +
                        ex.Message);
                }
            }
        }

        private string SafeFormatAsString(SCAPI.ModelProperty property)
        {
            try
            {
                return property.FormatAsString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetObjectName(SCAPI.ModelObject modelObject)
        {
            var possibleNameProperties = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "Attribute.Name",
        "Attribute.Physical.Name"
    };

            foreach (SCAPI.ModelProperty property in modelObject.Properties)
            {
                if (!possibleNameProperties.Contains(property.ClassName))
                    continue;

                try
                {
                    string value = RetrieveValue(property);

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                catch
                {
                }
            }

            return Convert.ToString(modelObject.ObjectId);
        }

        private bool IsEntityObject(SCAPI.ModelObject modelObject)
        {
            if (modelObject == null)
                return false;

            try
            {
                return string.Equals(
                    modelObject.ClassName,
                    "Entity",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public string CalculateVeriDegeri(
    string erisilebilirlik,
    string butunluk,
    string gizlilikSeviyesi)
        {
            var values = new[]
            {
        erisilebilirlik,
        butunluk,
        gizlilikSeviyesi
    };

            int maxValue = 1;

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string normalized = value
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("ç", "c")
                    .Replace("ğ", "g")
                    .Replace("ı", "i")
                    .Replace("ö", "o")
                    .Replace("ş", "s")
                    .Replace("ü", "u");

                int currentValue = 1;

                // Değer içerisinde doğrudan 1-4 arasında sayı varsa onu kullan.
                Match numberMatch = Regex.Match(normalized, @"(?<!\d)[1-4](?!\d)");

                if (numberMatch.Success)
                {
                    currentValue = int.Parse(numberMatch.Value);
                }
                else if (normalized.Contains("cok gizli") ||
                         normalized.Contains("yuksek"))
                {
                    currentValue = 4;
                }
                else if (normalized.Contains("gizli") ||
                         normalized.Contains("orta"))
                {
                    currentValue = 3;
                }
                else if (normalized.Contains("hizmete ozel") ||
                         normalized.Contains("dusuk"))
                {
                    currentValue = 2;
                }
                else if (normalized.Contains("kamuya acik") ||
                         normalized.Contains("bilgi"))
                {
                    currentValue = 1;
                }

                if (currentValue > maxValue)
                    maxValue = currentValue;
            }

            switch (maxValue)
            {
                case 4:
                    return "4-Çok Gizli/Yüksek";

                case 3:
                    return "3-Gizli/Orta";

                case 2:
                    return "2-Hizmete Özel/Düşük";

                default:
                    return "1-Kamuya Açık/Bilgi";
            }
        }

        public int CalculateBankaGoreceDegeri(
    string veriDegeri,
    string isSureciSeviyesi)
        {
            int veriDegeriNumber = 0;
            int isSureciNumber = 0;

            if (!string.IsNullOrWhiteSpace(veriDegeri))
            {
                Match match = Regex.Match(veriDegeri, @"(?<!\d)[1-4](?!\d)");

                if (match.Success)
                    int.TryParse(match.Value, out veriDegeriNumber);
            }

            if (!string.IsNullOrWhiteSpace(isSureciSeviyesi))
            {
                // İş süreci alanında birden fazla sayı bulunabilme ihtimaline karşı
                // son bulunan 1-4 arasındaki sayıyı kullanıyoruz.
                MatchCollection matches = Regex.Matches(
                    isSureciSeviyesi,
                    @"(?<!\d)[1-4](?!\d)");

                if (matches.Count > 0)
                {
                    int.TryParse(
                        matches[matches.Count - 1].Value,
                        out isSureciNumber);
                }
            }

            if (veriDegeriNumber == 0 || isSureciNumber == 0)
                return 0;

            return veriDegeriNumber * isSureciNumber;
        }

        public int CalculateGuvenlikSinifiDegeri(int veriDegeri,
    List<ScapiColumnInfo> columns,
    string hassasVeriMi,
    string sirKapsamindaVeriMi)
        {
            int kisiselVeriDegeri = 1;

            int HassasVeriDegeri = (string.Equals(
                   hassasVeriMi,
                   "True",
                   StringComparison.OrdinalIgnoreCase) ? 2 : 1);

            int sirKapsamindaVeriDegeri = (string.Equals(
                   sirKapsamindaVeriMi,
                   "True",
                   StringComparison.OrdinalIgnoreCase) ? 2 : 1);


            bool isKisiselVeri = false;

            foreach (ScapiColumnInfo column in columns)
            {
                if (column?.Properties == null)
                    continue;

                isKisiselVeri = column.Properties.Any(c =>
                    string.Equals(
                        c.ClassName,
                        "Attribute.Physical.Kisisel_Veri_Mi",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                        c.Format == "True"
                );

                if (isKisiselVeri)
                    break;
            }

            if (isKisiselVeri)
                kisiselVeriDegeri = 2;

            int res = veriDegeri * kisiselVeriDegeri * HassasVeriDegeri * sirKapsamindaVeriDegeri;

            ScapiTraceLogger.Info($"veriDegeri : {veriDegeri} * kisiselVeriDegeri : {kisiselVeriDegeri} * HassasVeriDegeri : {HassasVeriDegeri} * sirKapsamindaVeriDegeri : {sirKapsamindaVeriDegeri} = {res}");
            return res;
        }
    }
}
