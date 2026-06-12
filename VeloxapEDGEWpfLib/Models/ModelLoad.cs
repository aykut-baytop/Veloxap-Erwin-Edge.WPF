using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeloxapEDGEWpfLib.Models
{
    internal class ModelLoad
    {
        private SCAPI.Application oApplication;


        public ModelLoad(ref SCAPI.Application oApp)
        {
            oApplication = oApp;


        }
        public ModelLoad( )
        {
            oApplication = new SCAPI.Application();


        }
        public ModelInfo loadModel(SCAPI.PersistenceUnit oPersistenceUnit)
        {
            ModelInfo mModel = new ModelInfo();


            SCAPI.ModelObjects oSelectedCollection;
            SCAPI.Session oSession;

           
            SCAPI.SC_SessionLevel eLevel;


            if (oPersistenceUnit != null)
            {

                //
                /*
                 *  Filtrelerde kullanılan properitiesleri listeye ekleyerek sadece 
                 *  onları yükleyeceğiz. 
                 *  
                 *  
                 *  
                 */
                eLevel = SCAPI.SC_SessionLevel.SCD_SL_M0;
                oSession = oApplication.Sessions.Add();

           
                oSession.Open(oPersistenceUnit, eLevel);

                var objectlist = new[] { "Entity", "Relationship", "Attribute", "Sequence", "Key_Group", "Key_Group_Member" };
                oSelectedCollection = oSession.ModelObjects.Collect(oSession.ModelObjects.Root, null, 1);

                // Model genel bilgileri
                mModel.setoName(oPersistenceUnit.Name);
                mModel.setoObjectId(oSession.ModelObjects.Root.ObjectId);
                mModel.setoLocation(oPersistenceUnit.PropertyBag["Locator"].Value["Locator"]);


                // Model Object Property
                List<ObjectProperty> mObjectProperties = loadObjectProperities(true, oSession.ModelObjects.Root.ObjectId, null, oPersistenceUnit);
                mModel.setoObjectProperty(mObjectProperties);


                List<ModelObject> mModelObjects = new List<ModelObject>();


                foreach (SCAPI.ModelObject oObject in oSelectedCollection)
                {
                    ModelObject mModelObject = new ModelObject();

                    if (objectlist.Contains(oObject.ClassName))
                    {
                        mModelObject.setoObjectId(oObject.ObjectId);
                        mModelObject.setoClassName(oObject.ClassName);
                        mModelObject.setoName(oObject.Name);


                        // Model Object main properities (etc: D_Arac ve D_Arac özellikleri)
                        List<ObjectProperty> mObjectProperty = loadObjectProperities(false, oObject.ObjectId, oSession.ModelObjects.Root.ObjectId, oPersistenceUnit);


                        // D_Arac tablosunun sutunları ve sutunların özellikleri
                        mModelObject.setoModelObjects(loadSubModelObject(oObject.ObjectId, oPersistenceUnit));



                        mModelObject.setoObjectProperty(mObjectProperty);


                        mModelObjects.Add(mModelObject);



                    }


                }

                // Model Object
                mModel.setoModelObject(mModelObjects);

                oApplication.Sessions.Clear();
            }
            return mModel;
        }
        private List<ModelObject> loadSubModelObject(string objectId, SCAPI.PersistenceUnit oPersistenceUnit)
        {
            List<ModelObject> mModelObjects = new List<ModelObject>();


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
                ModelObject mModelObject = new ModelObject();

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
        private List<ObjectProperty> loadObjectProperities(bool isRoot, object objectId, object parentObjectId, SCAPI.PersistenceUnit oPersistenceUnit)
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
