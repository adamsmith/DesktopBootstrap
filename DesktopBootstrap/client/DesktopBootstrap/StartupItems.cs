using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DesktopBootstrap {

    internal static class StartupItems {

        internal static void DoStartup() {
            // this stuff all happens at startup, before we even know if this is a fresh install or not

            SafeInvokeDelegate(InitLog); // should go first
            SafeInvokeDelegate(InitUpdateChecker);
            SafeInvokeDelegate(InitAppShutdownLogger);
            SafeInvokeDelegate(LogCurrentCulture);
        }

        private static void InitLog() {
            Log.Init();
            Log.Logger.Trace("DesktopBootstrap starting up.  Logging initialized.",
                new OtherInfo("ClientGuid", LibraryRegistry.ClientGuid));
        }

        private static void SafeInvokeDelegate(SimpleDelegate x) {
            foreach (var target in x.GetInvocationList()) {
                try {
                    target.DynamicInvoke();
                } catch (Exception e) {
                    Log.Logger.ErrorException("Exception invoking delegate", e);
                }
            }
        }

        private static void InitUpdateChecker() {
            UpdateChecker.StartUpdateCheckerTimer(Library.BuildUpdateCheckRequest());
        }

        private static void InitAppShutdownLogger() {
            Application.ApplicationExit += Application_ApplicationExit;
        }

        private static void Application_ApplicationExit(object sender, EventArgs e) {
            Log.Logger.Trace("Application shutting down properly.");
        }

        private static void LogCurrentCulture() {
            Log.Logger.Debug(String.Format("Current culture is {0}",
                Thread.CurrentThread.CurrentUICulture.Name));
        }

    }
}
