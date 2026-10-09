using System;

namespace Veloxap.AddIn.Erwin.Models
{
    public class CustomUdpModel : ICloneable
    {
        public long Id { get; set; }

        public int ModelVersion { get; set; }

        public long ModelId { get; set; }

        public string ModelPath { get; set; }

        public string UdpName { get; set; }

        public string UdpVal { get; set; }

        public string Type { get; set; }

        public string ParentObject { get; set; }

        public string Object { get; set; }

        public string Environment { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
