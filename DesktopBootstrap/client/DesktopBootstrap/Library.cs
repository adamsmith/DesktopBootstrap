using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace DesktopBootstrap {

    public delegate void SimpleDelegate();

    internal static class Library {

        internal static bool IsDebugMode() {
#if DEBUG
            return true;
#endif

            return ((LibraryRegistry.GetStringValue(LibraryRegistry.PermanentRegistryPath, "IsDebug") ??
                string.Empty).ToLowerInvariant() == "true");
        }

        internal static UpdateCheckRequest BuildUpdateCheckRequest() {
            return new UpdateCheckRequest {
                AcceptTestCertificate = Library.IsDebugMode(),
                BeforeExecutingUpdateFile = TrayIconForm.HideForThirtySeconds,
                ClientGuid = LibraryRegistry.ClientGuid,
                LogMessageHandler = Log.Logger.Debug,
                LogWarningHandler = Log.Logger.WarnException,
                LogErrorHandler = Log.Logger.ErrorException,
                RequestIsFromService = false
            };
        }

    }

}
