namespace Veloxap.AddIn.Erwin.Models
{
    internal class ObjectProperty
    {
        private string oPropertyClassID;
        private string oPropertyClassName;
        private string oPropertyType;
        private string oPropertyFormatAsString;
        private string oPropertyValue;
        private string leftValue;
        private string rightValue;

        public void setLeftValue(string value)
        {
            leftValue = value;
        }

        public string getLeftValue()
        {
            return leftValue;
        }

        public void setRightValue(string value)
        {
            rightValue = value;
        }

        public string getRightValue()
        {
            return rightValue;
        }

        public void setObjectProperty(
            string oPropertyClassID,
            string oPropertyClassName,
            string oPropertyType,
            string oPropertyFormatAsString,
            string oPropertyValue)
        {
            this.oPropertyClassID = oPropertyClassID;
            this.oPropertyClassName = oPropertyClassName;
            this.oPropertyType = oPropertyType;
            this.oPropertyFormatAsString = oPropertyFormatAsString;
            this.oPropertyValue = oPropertyValue;
        }

        public void setoPropertyClassID(string oPropertyClassID)
        {
            this.oPropertyClassID = oPropertyClassID;
        }

        public string getoPropertyClassID()
        {
            return this.oPropertyClassID;
        }

        public void setoPropertyClassName(string oPropertyClassName)
        {
            this.oPropertyClassName = oPropertyClassName;
        }

        public string getoPropertyClassName()
        {
            return this.oPropertyClassName;
        }

        public void setoPropertyType(string oPropertyType)
        {
            this.oPropertyType = oPropertyType;
        }

        public string getoPropertyType()
        {
            return this.oPropertyType;
        }

        public void setoPropertyFormatAsString(string oPropertyFormatAsString)
        {
            this.oPropertyFormatAsString = oPropertyFormatAsString;
        }

        public string getoPropertyFormatAsString()
        {
            return this.oPropertyFormatAsString;
        }

        public void setoPropertyValue(string oPropertyValue)
        {
            this.oPropertyValue = oPropertyValue;
        }

        public string getoPropertyValue()
        {
            return this.oPropertyValue;
        }
    }
}
