using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace DesktopBootstrapService {
    
    public static class Program {

        public static void Main() {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]  { 
				new DesktopBootstrapService() 
			};
            ServiceBase.Run(ServicesToRun);
        }
    }
}
