using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Veloxap.AddIn.Erwin.Models
{
    internal class ModelInfo
    {

        private string oName;
        private string oObjectId;
        private string oLocation;
        private List<ModelObject> oModelObject;
        private List<ObjectProperty> oObjectProperty;


        public void setoModelObject(List<ModelObject> oModelObject)
        {
            this.oModelObject = oModelObject;
        }

        public List<ModelObject> getoModelObject()
        {
            return this.oModelObject;
        }

        public void setoObjectProperty(List<ObjectProperty> oObjectProperty)
        {
            this.oObjectProperty = oObjectProperty;
        }

        public List<ObjectProperty> getoObjectProperty()
        {
            return this.oObjectProperty;
        }

        public void setoName(string oName)
        {
            this.oName = oName;
        }

        public string getoName()
        {
            return this.oName;
        }
        public void setoObjectId(string oObjectId)
        {
            this.oObjectId = oObjectId;
        }

        public string getoObjectId()
        {
            return this.oObjectId;
        }
        public void setoLocation(string oLocation)
        {
            
            this.oLocation = oLocation;
        }

        public string getoLocation()
        {
            return this.oLocation;
        }
    }
}
