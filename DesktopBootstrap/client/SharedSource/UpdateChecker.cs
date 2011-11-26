using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Microsoft.Win32;

namespace DesktopBootstrap {

    internal delegate void VoidDelegate();
    internal delegate void LogMessage(string logMessage);
    internal delegate void LogException(string logMessage, Exception e);

    internal class UpdateCheckRequest {
        internal string ClientGuid { get; set; }
        internal bool RequestIsFromService { get; set; }
        internal bool AcceptTestCertificate { get; set; }
        internal LogMessage LogMessageHandler { get; set; }
        internal LogException LogWarningHandler { get; set; }
        internal LogException LogErrorHandler { get; set; }
        internal VoidDelegate BeforeExecutingUpdateFile { get; set; }
    }

    #region LibraryIO partial class

    internal enum CommonDirectories {
        CurrentExecutingDirectory,
        LocalAppData,
        Temp
    }

    internal static partial class LibraryIO {

        internal static bool CanWriteToDirectory(string dirPath) {
            try {
                if (!Directory.Exists(dirPath)) {
                    Directory.CreateDirectory(dirPath);
                }

                var tmpFilePath = Path.Combine(dirPath, new Random().Next().ToString());
                File.Create(tmpFilePath).Dispose();
                File.Delete(tmpFilePath);
                return true;
            } catch {
                return false;
            }
        }

        internal static string FindWritableDirectory(params CommonDirectories[] preferenceOrdering) {
            foreach (var candidate in preferenceOrdering) {
                string candidatePath;
                switch (candidate) {
                    case CommonDirectories.CurrentExecutingDirectory:
                        candidatePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        break;

                    case CommonDirectories.LocalAppData:
                        candidatePath = Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData), "DesktopBootstrap");
                        break;

                    case CommonDirectories.Temp:
                        candidatePath = Path.GetTempPath();
                        break;

                    default:
                        // I'd ordinarily throw an exception here, but I'm weary to because of
                        // this function's use in the updater.  Just skip unrecognized 
                        // CommonDirectories.
                        continue;
                }

                if (LibraryIO.CanWriteToDirectory(candidatePath)) {
                    return candidatePath;
                }
            }
            return null;
        }
    }

    #endregion

    internal static class UpdateChecker {

        private static readonly string k_updateUrl = "http://updates.DesktopBootstrap.com/updateCheck?build={0}&clientGuid={1}&requestIsFromService={2}";

        private static System.Threading.Timer s_checkTimer;

        internal static void StartUpdateCheckerTimer(UpdateCheckRequest requestData) {
            var timeUntilFirstCheck = new TimeSpan((long)(TimeSpan.FromHours(24).Ticks * new Random().NextDouble()));

            if (IsImmediateUpdateCheckFlagSet()) {
                timeUntilFirstCheck = TimeSpan.FromSeconds(10);

                SafeLogMessage(requestData, 
                    "ImmediateUpdateCheck flag is set, but will be subjugated to user preference for automatic updates.");
            }

            s_checkTimer = new System.Threading.Timer(CheckForAndRunUpdates_FromTimer, requestData,
                timeUntilFirstCheck, TimeSpan.FromHours(24));

            SafeLogMessage(requestData, AreAutomaticUpdatesEnabled() ?
                    "Automatic updates are enabled" : "Automatic updates are disabled (by user)");
            SafeLogMessage(requestData, String.Format(
                "First update check (if enabled) scheduled for t-minus {0} hours", 
                timeUntilFirstCheck.TotalHours));
        }

        internal static void ForceUpdateCheckNow(UpdateCheckRequest requestData, bool resetTimer) {
            if (resetTimer) {
                s_checkTimer.Change(TimeSpan.Zero, TimeSpan.FromHours(24));
            } else {
                CheckForAndRunUpdates(requestData);
            }
        }

        private static void CheckForAndRunUpdates_FromTimer(object requestDataObject) {
            if (!AreAutomaticUpdatesEnabled()) {
                SafeLogMessage(requestDataObject, 
                    "Skipping scheduled check for updates due to user setting.");
                return;
            }
            CheckForAndRunUpdates(requestDataObject);
        }

        // Note:   could block for a while, but will execute on a ThreadPool thread.
        // 
        // Note 2: don't let exceptions bubble past the scope of this function, or else bad things
        //         will happen since it's a ThreadPool thread on .NET 2.0.
        // 
        // Note 3: this function in theory should be reentrant, but is not since it's unlikely any
        //         single execution will last for longer than 24 hours (the time for subsequent 
        //         invocations).
        // 
        // Note 4: it could also be reentrant if the server commands ForceCheckForUpdates(), but that
        //         is also unlikely.
        private static void CheckForAndRunUpdates(object requestDataObject) {
            UpdateCheckRequest requestData = null;
            try {
                requestData = (requestDataObject as UpdateCheckRequest ?? new UpdateCheckRequest());

                // try to download update info
                var updateInfoXml = AskServerForUpdate(requestData);
                if (string.IsNullOrEmpty(updateInfoXml)) {
                    return;
                }

                // decode and verify update info
                var verifiedUpdateInfo = VerifiedUpdateInfo.FromUpdateXml(updateInfoXml,
                    (requestData == null ? false : requestData.AcceptTestCertificate));

                // try to download update executable
                var downloadFileDir = LibraryIO.FindWritableDirectory(CommonDirectories.CurrentExecutingDirectory,
                    CommonDirectories.LocalAppData, CommonDirectories.Temp);
                if (downloadFileDir == null) {
                    throw new Exception("Cannot find writable directory for update download.");
                }
                var downloadFilePath = Path.Combine(downloadFileDir, "DesktopBootstrapUpdater.exe");
                if (File.Exists(downloadFilePath)) {
                    try {
                        File.Delete(downloadFilePath);
                    } catch (IOException ioe) {
                        // this is probably an old update that didn't terminate for some reason.  try to kill it.

                        SafeLogMessage(requestData, "An updater seems to be running already.  Trying to kill it...");

                        int nKilled = 0;
                        foreach (var runningUpdater in Process.GetProcessesByName("DesktopBootstrapUpdater")) {
                            runningUpdater.Kill();
                            nKilled++;
                        }

                        SafeLogMessage(requestData, String.Format("Killed {0} already-running updaters", nKilled));
                    }
                }
                using (var webClient = new WebClient()) {
                    webClient.DownloadFile(verifiedUpdateInfo.DownloadUrl, downloadFilePath);
                }

                // verify downloaded file hash
                string downloadedFileHashBase64;
                using (var fileReader = new FileStream(downloadFilePath, FileMode.Open, FileAccess.Read)) {
                    downloadedFileHashBase64 = Convert.ToBase64String(new SHA1CryptoServiceProvider().ComputeHash(fileReader));
                }
                if (downloadedFileHashBase64 != verifiedUpdateInfo.DownloadHash) {
                    throw new Exception("Updater executable hash is mismatched.");
                }

                // we're about the run the updater.  if it sees fit it will TerminateProcess() us, which can
                // leave the tray icon laying behind well after we're gone, sometimes resulting in multiple
                // DesktopBootstrap icons in the system tray.  so instead we're going to hide our tray icon for
                // a little bit, in case the updater wants to kill us (ha!).
                // 
                // note, if this update code is executing on DesktopBootstrap.exe, the icon will just be hidden.
                // if it's being ran from the service, then DesktopBootstrap.exe will be asked to close gracefully,
                // which should remove the tray icon BUT if the update doesn't restart DesktopBootstrap.exe then it
                // won't be restarted until the next machine reboot.
                try {
                    if (requestData.BeforeExecutingUpdateFile != null) {
                        requestData.BeforeExecutingUpdateFile();
                    }
                } catch (Exception eBeforeExecuting) {
                    if (requestData != null && requestData.LogWarningHandler != null) {
                        ExecuteDelegateSwallowExceptions(requestData.LogWarningHandler,
                            "Exception calling BeforeExecutingUpdateFile delegate", eBeforeExecuting);
                    }
                }

                SafeLogMessage(requestData, String.Format("Launching update executable {0}", downloadFilePath));

                Process.Start(downloadFilePath);

            } catch (Exception e) {
                if (requestData != null && requestData.LogErrorHandler != null) {
                    ExecuteDelegateSwallowExceptions(requestData.LogErrorHandler,
                        "Exception while checking for and running updates", e);
                }
            }
        }

        private static string AskServerForUpdate(UpdateCheckRequest requestData) {
            int currentBuildNumber = Assembly.GetExecutingAssembly().GetName().Version.Revision;

            string clientGuid = string.Empty;
            if (requestData != null && requestData.ClientGuid != null) {
                clientGuid = requestData.ClientGuid;
            }

            bool requestIsFromService = false;
            if (requestData != null) {
                requestIsFromService = requestData.RequestIsFromService;
            }

            // Do initial HTTP GET request
            var request = (HttpWebRequest)WebRequest.Create(
                String.Format(k_updateUrl, currentBuildNumber, clientGuid, requestIsFromService));
            request.KeepAlive = false;
            try {
                using (var responseObject = request.GetResponse())
                using (var responseReader = new StreamReader(responseObject.GetResponseStream())) {
                    return responseReader.ReadToEnd();
                }
            } catch (Exception e) {
                ExecuteDelegateSwallowExceptions(requestData.LogWarningHandler,
                    "Exception while trying to check for update info.", e);
                return null;
            }
        }

        private class VerifiedUpdateInfo {

            internal string DownloadUrl { get; set; }
            internal string DownloadHash { get; set; }

            internal static VerifiedUpdateInfo FromUpdateXml(string updateXmlString, bool acceptTestCertificate) {
                // This could probably be refactored to be more efficient, etc, but I wanted to avoid
                // rethrowing exceptions or making further assumptions about how FromUpdateXml(string, string)'s
                // xml handling and verification works.
                if (acceptTestCertificate) {
                    try {
                        return FromUpdateXml(updateXmlString, k_certificate_PROD);
                    } catch {
                        return FromUpdateXml(updateXmlString, k_certificate_TEST);
                    }
                } else {
                    return FromUpdateXml(updateXmlString, k_certificate_PROD);
                }
            }

            private static VerifiedUpdateInfo FromUpdateXml(string updateXmlString, string certString) {
                var base64Cert = certString.Replace("\r", "").Replace("\n", "").Replace(
                    "-----BEGIN CERTIFICATE-----", "").Replace("-----END CERTIFICATE-----", "");
                X509Certificate2 cert = new X509Certificate2(UTF8Encoding.UTF8.GetBytes(base64Cert));

                var xml = new XmlDocument();
                xml.PreserveWhitespace = true;
                xml.LoadXml(updateXmlString);

                var signedXml = new SignedXml(xml);
                var signatureNodeList = xml.GetElementsByTagName("Signature");
                signedXml.LoadXml((XmlElement)signatureNodeList[0]);

                if (!signedXml.CheckSignature(cert, true)) {
                    throw new Exception("Signature verification failed.");
                }

                return new VerifiedUpdateInfo {
                    DownloadUrl = xml["UpdateInfo"]["DownloadUrl"].InnerText,
                    DownloadHash = xml["UpdateInfo"]["DownloadHash"].InnerText
                };
            }
        }

        #region Helper Functions

        private static bool IsImmediateUpdateCheckFlagSet() {
            try {
                using (var regKey = Registry.LocalMachine.OpenSubKey(@"Software\DesktopBootstrap", false)) {
                    if (regKey == null) {
                        return false;
                    }
                    return (((regKey.GetValue("ImmediateUpdateCheck") as string) ?? string.Empty).ToLowerInvariant() ==
                        "true");
                }
            } catch {
                return false;
            }
        }

        private static bool AreAutomaticUpdatesEnabled() {
            return (AreAutomaticUpdatesEnabled(Registry.LocalMachine) && 
                AreAutomaticUpdatesEnabled(Registry.CurrentUser));
        }

        private static bool AreAutomaticUpdatesEnabled(RegistryKey regHive) {
            try {
                using (var regKey = regHive.OpenSubKey(@"Software\DesktopBootstrap\AppData", false)) {
                    if (regKey == null) {
                        return true;
                    }
                    return ((regKey.GetValue("AutomaticUpdatesEnabled") as long?).GetValueOrDefault(1) != 0);
                }
            } catch {
                return true;
            }
        }

        private static void ExecuteDelegateSwallowExceptions(Delegate callee, params object[] parameters) {
            // If e.g. the logging code throws an exception, don't let it prevent us from updating.

            try {
                callee.DynamicInvoke(parameters);
            } catch { }
        }

        private static void SafeLogMessage(object requestDataObject, string logMessage) {
            UpdateCheckRequest requestData = (requestDataObject as UpdateCheckRequest);

            if (requestData != null && requestData.LogMessageHandler != null) {
                ExecuteDelegateSwallowExceptions(requestData.LogMessageHandler, logMessage);
            }
        }

        #endregion

        #region DesktopBootstrap update certificates

        private static readonly string k_certificate_PROD =
@"note: generate your own PROD key, then paste the contents of certificate.pem into here";

        private static readonly string k_certificate_TEST =
@"-----BEGIN CERTIFICATE-----
MIIDBjCCAe4CCQCGC3GoFKkwUzANBgkqhkiG9w0BAQUFADBFMQswCQYDVQQGEwJB
VTETMBEGA1UECBMKU29tZS1TdGF0ZTEhMB8GA1UEChMYSW50ZXJuZXQgV2lkZ2l0
cyBQdHkgTHRkMB4XDTExMDkyNzIwMzUyM1oXDTMzMDgyMjIwMzUyM1owRTELMAkG
A1UEBhMCQVUxEzARBgNVBAgTClNvbWUtU3RhdGUxITAfBgNVBAoTGEludGVybmV0
IFdpZGdpdHMgUHR5IEx0ZDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEB
AM4wXxFQmAVBUSSt7mebab34ZPXzlbUwMjcXVnigIPAp+RuVXKiCbYdJth0D9vQq
LOmjpiAAAxQYu2UgtP0fDTi7LCZjpht/749wYolZHj8VLb4b9wW1Nkm3JwYony90
H9MO16rZlVuIFTW6JY+JHqTp4uq4yH7w7fF9moNAM/q8nnLNDZXCbfp025gl5Bm+
fhyYeHGGoSs9QG5+aloW8Ikz9If1DWV/0Ijh4P8cD18ZmepCxNq8rvB6wMYZcf7P
Y47+RJ/39a+AmzGFpjoWbWXHxL4vHTFO15Hg2pwuNuW8TmjtaIbiGEXB85dkBlNk
jMpJgjK+uEX5Nz9loC6KExECAwEAATANBgkqhkiG9w0BAQUFAAOCAQEAY5UVyuDZ
vDZ10NWo+/WjqZVAniL3KXlVMljZlgkC/nnkRnS/hAsAEWMlr+JTVNeGObE3s9fi
CJJz0pehl0F4GTnJ9z4EbZ/1obIZ3vTFDynxRTVKZQQ6WYgAZ44JFVIMkgzmTusZ
o/jG6jWlsTbqR7jiSTf5/AMYr+u57G/FZ1J29T3ZivW2KPVzFsfn6+ZFzphuNToJ
5dPRU5fdmcAwFfTLC3dpEJt6c+sbzLK1IiNR5YJjG5Fsy2iAVJqac+eGV/vysq0a
iB0BXi6wj/cbu8R5aM+iFTViMFGD0O1bPX06GGv6b7+HgnB9YmFWeElAZzl6o+nu
wUg8JgkK9s+iKQ==
-----END CERTIFICATE-----";

        #endregion
    }
}
