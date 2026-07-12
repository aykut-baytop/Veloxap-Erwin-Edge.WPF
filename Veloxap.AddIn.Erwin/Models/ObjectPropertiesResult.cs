using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Veloxap.AddIn.Erwin.Models
{
    public class ObjectPropertiesResult
    {
        public List<ScapiPropertyInfo> EntityProperties { get; set; }
            = new List<ScapiPropertyInfo>();

        public List<ScapiColumnInfo> Columns { get; set; }
            = new List<ScapiColumnInfo>();
    }

    public class ScapiColumnInfo
    {
        public string ObjectId { get; set; }
        public string Name { get; set; }

        public List<ScapiPropertyInfo> Properties { get; set; }
            = new List<ScapiPropertyInfo>();
    }

    public class ScapiPropertyInfo
    {
        public string ClassName { get; set; }
        public string DataType { get; set; }
        public string Format { get; set; }
        public string Value { get; set; }
    }
}
