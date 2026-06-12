using SCAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using VeloxapEDGErwinTools.AddIn.Model;
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

        public List<(string, string,string)> getModelsNamePath()
        {

            if (oApplication == null) return null;

            List<(string value,string key1, string key2 )> oModelsName = new List<(string, string,string)>();

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
                    MessageBox.Show(e.ToString());

                    return null;
                }
                oApplication.Sessions.Clear();

                oModelsName.Add((sTitle,sObjectId, pObjectId ));
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
            foreach (SCAPI.PersistenceUnit oUnit in oPersistenceUnits)
            {
                index++;
                if (oUnit.ObjectId == pobjectId){
                    break;
                }
                

            } 
            if(index>=0)
            { 

                //
                /*
                 *  Filtrelerde kullanılan properitiesleri listeye ekleyerek sadece 
                 *  onları yükleyeceğiz. 
                 *  
                 *  
                 *  
                 */
             //   eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
              //  oSession = oApplication.Sessions.Add();

                oPersistenceUnit = oApplication.PersistenceUnits[index]; // combo box level
                ModelLoad mLoad = new ModelLoad();

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
                List<Model.ObjectProperty> mObjectProperties = loadObjectProperities(true, oSession.ModelObjects.Root.ObjectId, null, oPersistenceUnit);
                mModel.setoObjectProperty(mObjectProperties);


                List<Model.ModelObject > mModelObjects = new List<Model.ModelObject>();


                foreach (SCAPI.ModelObject oObject in oSelectedCollection)
                {
                    Model.ModelObject mModelObject = new Model.ModelObject();

                    if (objectlist.Contains(oObject.ClassName)  )
                    {
                        mModelObject.setoObjectId(oObject.ObjectId);
                        mModelObject.setoClassName(oObject.ClassName);
                        mModelObject.setoName(oObject.Name);


                        // Model Object main properities (etc: D_Arac ve D_Arac özellikleri)
                        List<Model.ObjectProperty> mObjectProperty = loadObjectProperities(false, oObject.ObjectId, oSession.ModelObjects.Root.ObjectId, oPersistenceUnit);


                        // D_Arac tablosunun sutunları ve sutunların özellikleri
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
        public List<Model.ModelObject> loadSubModelObject(string objectId, SCAPI.PersistenceUnit oPersistenceUnit)
        {
            List<Model.ModelObject> mModelObjects = new List<Model.ModelObject>();


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
                Model.ModelObject mModelObject = new Model.ModelObject();
                
                if (objectlist.Contains(oObject.ClassName) )
                {
                    mModelObject.setoObjectId(oObject.ObjectId);
                    mModelObject.setoClassName(oObject.ClassName);
                    mModelObject.setoName(oObject.Name);

                    
                    List<Model.ObjectProperty> mObjectProperty = loadObjectProperities(false, oObject.ObjectId, oSession.ModelObjects.Root.ObjectId, oPersistenceUnit);
                    mModelObject.setoObjectProperty(mObjectProperty);
                    mModelObjects.Add(mModelObject);

                   

                }


            }


            return mModelObjects;
        }
        public List<Model.ObjectProperty> loadObjectProperities(bool isRoot, object objectId, object parentObjectId, SCAPI.PersistenceUnit oPersistenceUnit)
        {

            List<Model.ObjectProperty> mObjectProperties = new List<ObjectProperty>();

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
                        Model.ObjectProperty mObjectProperty = new Model.ObjectProperty();
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
            List<(string, string, string)> modelObjectsList = new List<(string, string, string)>();

            SCAPI.ModelObjects oSelectedCollection;
            SCAPI.Session oSession;

            SCAPI.PersistenceUnit oPersistenceUnit;
            SCAPI.SC_SessionLevel eLevel;



            eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
            oSession = oApplication.Sessions.Add();

            oPersistenceUnit = oApplication.PersistenceUnits[selectedModelIndex]; // combo box level

            oSession.Open(oPersistenceUnit, eLevel);

            var objectlist = new[] { "Entity", "Relationship", "Attribute", "Sequence", "Key_Group", "Key_Group_Member" };
            oSelectedCollection = oSession.ModelObjects.Collect(objectId, null, 1);
            //modelObjectsList.Add(("Model", oPersistenceUnit.Name, oPersistenceUnit.ObjectId));

            foreach (SCAPI.ModelObject oObject in oSelectedCollection)
            {
                if (objectlist.Contains(oObject.ClassName)   )
                    modelObjectsList.Add((oObject.ClassName, oObject.Name, oObject.ObjectId));


            }
            oApplication.Sessions.Clear();
            return modelObjectsList;
        }

        public List<(string, string, string, string)> getObjectProperities(bool isRoot, object objectId, object parentObjectId, int selectedModelIndex)
        {
            List<(string, string, string, string)> objectProperities = new List<(string, string, string, string)>();
            SCAPI.Session oSession;
            SCAPI.ModelObject oRootObject;
            SCAPI.ModelObject oObject;

            SCAPI.PersistenceUnit oPersistenceUnit;
            SCAPI.SC_SessionLevel eLevel;

            eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
            oSession = oApplication.Sessions.Add();
            
            try
            {
                oPersistenceUnit = oApplication.PersistenceUnits[selectedModelIndex];
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
                        try
                        {
                            
                            string type = PropertyDataType(oProperty);
                            string format = oProperty.FormatAsString();
                            string val = RetrieveValue(oProperty);
                            
                            objectProperities.Add((oProperty.ClassName, type, format, val));


                        }
                        catch (Exception e) { }


                    }



                }
            }
            catch (Exception e)
            {

            }
            oApplication.Sessions.Clear();
            return objectProperities;
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
    }
}
