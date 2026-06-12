using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace VeloxapEDGEWpfLib.Models
{
    internal class ModelObject
    {
        private string oObjectId;
        private string oClassName;
        private string oName;
        private List<ObjectProperty> oObjectProperty;
        private List<ModelObject> oModelObjects;

        public ModelObject()
        {
            oObjectProperty = new List<ObjectProperty>();
            oObjectId = "";
            oClassName = "";
            oName = "";

        }

        public void setoModelObjects(List<ModelObject> oModelObjects)
        {
            this.oModelObjects = oModelObjects;
        }

        public List<ModelObject> getoModelObject()
        {
            return this.oModelObjects;
        }

        public void setoObjectProperty(List<ObjectProperty> oObjectProperty)
        {
            this.oObjectProperty = oObjectProperty;
        }

        public List<ObjectProperty> getoObjectProperty()
        {
            return this.oObjectProperty;
        }
        public void setModelObject(string oObjectId, string oClassName, string oName)
        {
            this.oObjectId = oObjectId;
            this.oClassName = oClassName;
            this.oName = oName;
           
        }
        public void setoObjectId(string oObjectId)
        {
            this.oObjectId = oObjectId;
        }

        public string getoObjectId()
        {
            return this.oObjectId;
        }
        public void setoClassName(string oClassName)
        {
            this.oClassName = oClassName;
        }

        public string getoClassName()
        {
            return this.oClassName;
        }

        public void setoName(string oName)
        {
            this.oName = oName;
        }

        public string getoName()
        {
            return this.oName;
        }

    }
}
