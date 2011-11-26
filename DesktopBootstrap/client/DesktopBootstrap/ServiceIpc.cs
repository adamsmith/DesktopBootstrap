using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using System.Threading;

namespace DesktopBootstrap {

    internal class ServiceIpc {

        internal static void SignalEnableAutoUpdates() {
            SignalGlobalEvent(@"Global\DesktopBootstrap-EnableAutomaticUpdates");
        }

        internal static void SignalDisableAutoUpdates() {
            SignalGlobalEvent(@"Global\DesktopBootstrap-EnableAutomaticUpdates");
        }

        internal static void SignalEnableWindowsStartup() {
            SignalGlobalEvent(@"Global\DesktopBootstrap-EnableWindowsStartup");
        }

        internal static void SignalDisableWindowsStartup() {
            SignalGlobalEvent(@"Global\DesktopBootstrap-DisableWindowsStartup");
        }

        private static void SignalGlobalEvent(string name) {
            EventWaitHandle eventHandle = null;
            try {
                eventHandle = EventWaitHandle.OpenExisting(name, EventWaitHandleRights.Modify);
                eventHandle.Set();
            } catch (Exception e) {
                Log.Logger.ErrorException("Exception signaling global event", e);
            } finally {
                if (eventHandle != null) {
                    eventHandle.Close();
                }
            }
        }
    }
}
