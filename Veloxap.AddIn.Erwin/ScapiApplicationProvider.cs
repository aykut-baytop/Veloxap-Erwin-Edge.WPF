using System;
using System.Runtime.InteropServices;

namespace Veloxap.AddIn.Erwin
{
    internal static class ScapiApplicationProvider
    {
        private static readonly string[] ActiveApplicationProgIds =
        {
            "erwin9.SCAPI",
            "erwin9.SCAPI.9.0",
            "AllFusionErwin.SCAPI"
        };

        public static SCAPI.Application GetApplication()
        {
            foreach (string progId in ActiveApplicationProgIds)
            {
                SCAPI.Application activeApplication = TryGetActiveApplication(progId);
                if (activeApplication != null)
                    return activeApplication;
            }

            return null;
        }

        private static SCAPI.Application TryGetActiveApplication(string progId)
        {
            if (string.IsNullOrWhiteSpace(progId))
                return null;

            try
            {
                object activeObject = Marshal.GetActiveObject(progId);
                SCAPI.Application application = activeObject as SCAPI.Application;
                if (application != null)
                    return application;

                return (SCAPI.Application)activeObject;
            }
            catch
            {
                return null;
            }
        }
    }
}
