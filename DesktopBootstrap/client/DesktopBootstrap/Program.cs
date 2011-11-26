using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace DesktopBootstrap {

    public static class Program {

        private static Mutex s_instanceMutex = null;

        [STAThread]
        public static void Main() {
            try {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool createdNew;
                s_instanceMutex = new Mutex(true, "DesktopBootstrapClientInstance", out createdNew);
                if (!createdNew) {
                    if (Library.IsDebugMode()) {
                        OutputDebugString("DesktopBootstrap is already running.  Exiting this instance.");
                    }

                    return;
                }

                StartupItems.DoStartup();

                // note about tray icon forms:
                // we need to use the Application.Run(Form) overload so that if the form gets a WM_CLOSE
                // (e.g. from the service) then our application terminates as well.  if you instead
                // do Form.Show() and Application.Run() (with no args), the application won't close
                // if the form ever closes, receives WM_CLOSE, etc.  You _can_ make multiple, serial calls
                // to Application.Run(Form).

                // run main thread event loop
                Application.Run(new TrayIconForm());

            } catch (ThreadAbortException) {
                // do nothing -- this exception will rethrow automatically
            } catch (Exception e) {
                OutputDebugString("DesktopBootstrap: Exception on main event loop.  Rethrowing and quiting.");
                throw;
            } finally {
                if (s_instanceMutex != null) {
                    s_instanceMutex.ReleaseMutex();
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern void OutputDebugString(string message);

    }
}
