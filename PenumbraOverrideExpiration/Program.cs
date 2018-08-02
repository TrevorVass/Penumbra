using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PenumbraOverrideExpiration
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            // Allows this to be run as a console application or as a service.
            if (Environment.UserInteractive)
            {
                PenumbraOverrideExpirationService penumbraService = new PenumbraOverrideExpirationService();
                penumbraService.TestStartupAndStop(new string[] { });
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new PenumbraOverrideExpirationService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
