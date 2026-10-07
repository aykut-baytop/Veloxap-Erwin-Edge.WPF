using System;

namespace Veloxap.AddIn.Erwin.Models
{
    public class VeloxapServiceBaseResponse
    {
        public object Data { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }
    }
}
