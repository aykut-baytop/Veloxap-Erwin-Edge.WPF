using System;

namespace Veloxap.AddIn.Erwin.Models
{
    public sealed class GetCustomUdpRequest
    {
        public long ModelId { get; set; }

        public string UdpName { get; set; }

        public string Type { get; set; }

        public string ParentObject { get; set; }

        public string Object { get; set; }

        public string Environment { get; set; }
    }
}
