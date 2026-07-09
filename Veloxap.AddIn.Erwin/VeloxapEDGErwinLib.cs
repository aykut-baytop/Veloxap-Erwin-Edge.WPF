using SCAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Veloxap.AddIn.Erwin.Models;
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

        public int getNumberOfModels()
        {
            if (oApplication == null || oApplication.PersistenceUnits == null)
                return 0;

            return oApplication.PersistenceUnits.Count;
        }

        public SCAPI.PersistenceUnit getPersistenceUnit(int modelIndex)
        {
            if (oApplication == null || oApplication.PersistenceUnits == null)
                return null;

            if (modelIndex < 0 || modelIndex >= oApplication.PersistenceUnits.Count)
                return null;

            return oApplication.PersistenceUnits[modelIndex];
        }

        public ModelInfo loadModelObjectForIntegrate(int modelIndex)
        {
            try
            {
                SCAPI.PersistenceUnit oPersistenceUnit = getPersistenceUnit(modelIndex);
                if (oPersistenceUnit == null)
                    return null;

                ModelLoad mLoad = new ModelLoad(ref oApplication);
                return mLoad.loadModel(oPersistenceUnit);
            }
            catch
            {
                return null;
            }
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

        public List<(string, string,string)> getModelsNamePath()
        {

            if (oApplication == null) return null;

            List<(string value,string key1, string key2 )> oModelsName = new List<(string, string,string)>();

            SCAPI.PropertyBag oBag;
            String sTitle;
            String sLocation;
            String sObjectId;
            String pObjectId;

            foreach (PersistenceUnit oUnit in oApplication.PersistenceUnits)
            {
                try
                {
                    pObjectId = oUnit.ObjectId;
                    sTitle = string.IsNullOrWhiteSpace(oUnit.Name)
                        ? pObjectId
                        : oUnit.Name;
                    sObjectId = string.Empty;
                    oBag = oUnit.PropertyBag["Locator;Hidden_Model"];

                    sLocation = oBag.Value["Locator"]; //Get the location
                    if (sLocation.Length > 0)
                        sTitle = sTitle + " (" + sLocation + ")";

                    if (oBag.Value["Hidden_Model"])
                        sTitle = sTitle + " [Hidden]"; //Check if the persistence unit is hidden

                    oBag.ClearAll();

                    oModelsName.Add((sTitle,sObjectId, pObjectId ));
                }
                catch
                {
                    //MessageBox.Show(e.ToString());
                    // uyari

                    return null;
                }
            }

            return oModelsName;

        }
        public ModelInfo loadModelObject(string objectId, string pobjectId)
        {
            SCAPI.PersistenceUnit oPersistenceUnit = findPersistenceUnit(pobjectId);
            if (oPersistenceUnit == null)
                return new ModelInfo();

            ModelLoad mLoad = new ModelLoad(ref oApplication);
            return mLoad.loadModel(oPersistenceUnit);
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
            SCAPI.Session oSession = null;

            SCAPI.SC_SessionLevel eLevel;



            try
            {
                eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
                oSession = oApplication.Sessions.Add();

                oSession.Open(oPersistenceUnit, eLevel);

                var objectlist = new[] { "Entity", "Relationship", "Attribute", "Sequence", "Key_Group", "Key_Group_Member" };
                if (string.IsNullOrWhiteSpace(objectId))
                    objectId = oSession.ModelObjects.Root.ObjectId;

                oSelectedCollection = oSession.ModelObjects.Collect(objectId, null, 1);

                foreach (SCAPI.ModelObject oObject in oSelectedCollection)
                {
                    Veloxap.AddIn.Erwin.Models.ModelObject mModelObject = new Veloxap.AddIn.Erwin.Models.ModelObject();

                    if (objectlist.Contains(oObject.ClassName))
                    {
                        mModelObject.setoObjectId(oObject.ObjectId);
                        mModelObject.setoClassName(oObject.ClassName);
                        mModelObject.setoName(oObject.Name);

                        List<ObjectProperty> mObjectProperty = ReadObjectProperties(oObject);
                        mModelObject.setoObjectProperty(mObjectProperty);
                        mModelObjects.Add(mModelObject);
                    }

                }
            }
            catch
            {
            }
            finally
            {
                CloseSession(oSession);
            }


            return mModelObjects;
        }
        public List<ObjectProperty> loadObjectProperities(bool isRoot, object objectId, object parentObjectId, SCAPI.PersistenceUnit oPersistenceUnit)
        {

            List<ObjectProperty> mObjectProperties = new List<ObjectProperty>();

            SCAPI.Session oSession = null;
            SCAPI.ModelObject oRootObject;
            SCAPI.ModelObject oObject;

            SCAPI.SC_SessionLevel eLevel;

            try
            {
                eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
                oSession = oApplication.Sessions.Add();
                oSession.Open(oPersistenceUnit, eLevel);
               

                if (isRoot)
                    oObject = oSession.ModelObjects.Root;
                else
                {
                    try
                    {
                        oObject = oSession.ModelObjects[objectId];
                    }
                    catch
                    {
                        oRootObject = oSession.ModelObjects[parentObjectId];
                        oObject = oSession.ModelObjects.Collect(oRootObject)[objectId];
                    }
                }

                if (oObject != null)
                    mObjectProperties.AddRange(ReadObjectProperties(oObject));
            }
            catch
            {

            }
            finally
            {
                CloseSession(oSession);
            }

            return mObjectProperties;
        }

        private List<ObjectProperty> ReadObjectProperties(SCAPI.ModelObject oObject)
        {
            List<ObjectProperty> mObjectProperties = new List<ObjectProperty>();

            if (oObject == null)
                return mObjectProperties;

            try
            {
                foreach (SCAPI.ModelProperty oProperty in oObject.Properties)
                {
                    try
                    {
                        ObjectProperty mObjectProperty = new ObjectProperty();

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
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return mObjectProperties;
        }

        public List<(string, string, string)> getModelObjects(string objectId, int selectedModelIndex)
        {
            List<(string, string, string)> modelObjectsList = new List<(string, string, string)>();

            SCAPI.ModelObjects oSelectedCollection;
            SCAPI.Session oSession = null;

            SCAPI.PersistenceUnit oPersistenceUnit;
            SCAPI.SC_SessionLevel eLevel;



            try
            {
                if (oApplication == null ||
                    oApplication.PersistenceUnits == null ||
                    selectedModelIndex < 0 ||
                    selectedModelIndex >= oApplication.PersistenceUnits.Count)
                {
                    return modelObjectsList;
                }

                eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
                oSession = oApplication.Sessions.Add();

                oPersistenceUnit = oApplication.PersistenceUnits[selectedModelIndex]; // combo box level

                oSession.Open(oPersistenceUnit, eLevel);

                var objectlist = new[] { "Entity", "Relationship", "Attribute", "Sequence", "Key_Group", "Key_Group_Member" };
                oSelectedCollection = oSession.ModelObjects.Collect(objectId, null, 1);
                //modelObjectsList.Add(("Model", oPersistenceUnit.Name, oPersistenceUnit.ObjectId));

                foreach (SCAPI.ModelObject oObject in oSelectedCollection)
                {
                    if (objectlist.Contains(oObject.ClassName))
                        modelObjectsList.Add((oObject.ClassName, oObject.Name, oObject.ObjectId));
                }
            }
            catch
            {
            }
            finally
            {
                CloseSession(oSession);
            }

            return modelObjectsList;
        }

        public List<(string, string, string, string)> getObjectProperities(bool isRoot, object objectId, object parentObjectId, int selectedModelIndex)
        {
            List<(string, string, string, string)> objectProperities = new List<(string, string, string, string)>();
            SCAPI.Session oSession = null;
            SCAPI.ModelObject oRootObject;
            SCAPI.ModelObject oObject;

            SCAPI.PersistenceUnit oPersistenceUnit;
            SCAPI.SC_SessionLevel eLevel;

            eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
            
            try
            {
                if (oApplication == null ||
                    oApplication.PersistenceUnits == null ||
                    selectedModelIndex < 0 ||
                    selectedModelIndex >= oApplication.PersistenceUnits.Count)
                {
                    return objectProperities;
                }

                oSession = oApplication.Sessions.Add();
                oPersistenceUnit = oApplication.PersistenceUnits[selectedModelIndex];
                oSession.Open(oPersistenceUnit, eLevel);

                if (isRoot)
                {
                    oObject = oSession.ModelObjects.Root;
                }
                else
                {
                    try
                    {
                        oObject = oSession.ModelObjects[objectId];
                    }
                    catch
                    {
                        oRootObject = oSession.ModelObjects[parentObjectId];
                        oObject = oSession.ModelObjects.Collect(oRootObject)[objectId];
                    }
                }

                if (oObject != null)
                {
                    foreach (SCAPI.ModelProperty oProperty in oObject.Properties)
                    {
                        try
                        {
                            
                            string type = PropertyDataType(oProperty);
                            string format = oProperty.FormatAsString();
                            string val = RetrieveValue(oProperty);
                            
                            objectProperities.Add((oProperty.ClassName, type, format, val));


                        }
                    catch { }


                    }



                }
            }
            catch
            {

            }
            finally
            {
                CloseSession(oSession);
            }

            return objectProperities;
        }

        private void CloseSession(SCAPI.Session session)
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

            if (oApplication == null || oApplication.Sessions == null)
                return;

            try
            {
                oApplication.Sessions.Remove(session);
            }
            catch
            {
            }
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
            catch
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
            catch
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
    }
}
