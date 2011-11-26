using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using DesktopBootstrap;
using Microsoft.Win32;

namespace DesktopBootstrapService {

    public partial class DesktopBootstrapService : ServiceBase {

        internal static readonly string PermanentRegistryPath = @"Software\DesktopBootstrap";
        internal static readonly string PerInstallationRegistryPath = @"Software\DesktopBootstrap\AppData";

        public DesktopBootstrapService() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {
            // warning: be careful about putting anything in here -- we've seen instances
            // of the service getting killed by the SCM (service control manager) because
            // it was taking > 30 seconds to return.  that may seem like a lot, but it's not
            // if the machine and 10's of other services are trying to boot at the same
            // time!
            // 
            // note/update: the startup timeout might not have been due to logic in this 
            // function.  it was probably actually due to authenticode verification which
            // took HTTP calls that might have been timing out / no connection.  I fixed
            // this by specifying generatePublisherEvidence in app.config, per these URLs:
            // * http://msdn.microsoft.com/en-us/library/bb629393.aspx (note comment RE 
            //   services)
            // * http://blogs.msdn.com/b/dougste/archive/2008/02/29/should-i-authenticode-sign-my-net-assembly.aspx
            // 
            // nevertheless, it's probably still good practice to return as quickly as 
            // possible.

            // two minutes is supposedly a maximum (http://bit.ly/kZPSUk), but who knows.
            // should be a significant improvement.
            RequestAdditionalTime(120000);

            // execute StartupForReal asynchronously
            ThreadPool.QueueUserWorkItem(StartupForReal, null);
        }

        protected override void OnStop() {
            LogMessage("DesktopBootstrapService stopped");
        }

        private void StartupForReal(object unusedState) {
            LogMessage("DesktopBootstrapService started");

            try {
                UpdateChecker.StartUpdateCheckerTimer(new UpdateCheckRequest {
                    AcceptTestCertificate = ShouldAcceptTestCertificate(),
                    BeforeExecutingUpdateFile = BeforeExecutingUpdateFile,
                    ClientGuid = GetClientGuidAndCreateIfNecessary(),
                    RequestIsFromService = true,
                    LogMessageHandler = LogMessage,
                    LogErrorHandler = LogError,
                    LogWarningHandler = LogWarning
                });

                CheckForAllClientAppUserSessionsFromUpdate();

                StartListeningForEventFromAnyClientApp();

            } catch (Exception e) {
                LogError("Exception during service startup", e);
            }
        }

        #region Listening for win32 event RE AutomaticUpdatesEnabled & StartWithWindows

        private EventWaitHandle m_enableAutomaticUpdatesEvent;
        private EventWaitHandle m_disableAutomaticUpdatesEvent;

        private EventWaitHandle m_enableWindowsStartupEvent;
        private EventWaitHandle m_disableWindowsStartupEvent;

        private void StartListeningForEventFromAnyClientApp() {
            try {
                // Note: don't change these names without also changing them in the client app
                m_enableAutomaticUpdatesEvent = CreateGlobalEventWaitHandle(
                    @"Global\DesktopBootstrap-EnableAutomaticUpdates");
                m_disableAutomaticUpdatesEvent = CreateGlobalEventWaitHandle(
                    @"Global\DesktopBootstrap-DisableAutomaticUpdates");

                m_enableWindowsStartupEvent = CreateGlobalEventWaitHandle(
                    @"Global\DesktopBootstrap-EnableWindowsStartup");
                m_disableWindowsStartupEvent = CreateGlobalEventWaitHandle(
                    @"Global\DesktopBootstrap-DisableWindowsStartup");

                ThreadPool.QueueUserWorkItem(WaitForRegistryEventFromClient, m_enableAutomaticUpdatesEvent);
                ThreadPool.QueueUserWorkItem(WaitForRegistryEventFromClient, m_disableAutomaticUpdatesEvent);

                ThreadPool.QueueUserWorkItem(WaitForRegistryEventFromClient, m_enableWindowsStartupEvent);
                ThreadPool.QueueUserWorkItem(WaitForRegistryEventFromClient, m_disableWindowsStartupEvent);
            } catch (Exception e) {
                LogError("Error starting listening for automatic update signaling events", e);
            }
        }

        private void WaitForRegistryEventFromClient(object eventWaitHandleToProcess) {
            try {
                EventWaitHandle handle = (EventWaitHandle)eventWaitHandleToProcess;
                while (true) {
                    handle.WaitOne();

                    if (handle == m_enableAutomaticUpdatesEvent || handle == m_disableAutomaticUpdatesEvent) {
                        // should we enable or disable automatic updates?
                        bool newRegistryValue = (handle == m_enableAutomaticUpdatesEvent);

                        using (var regKey = Registry.LocalMachine.CreateSubKey(PerInstallationRegistryPath)) {
                            if (regKey != null) {
                                regKey.SetValue("AutomaticUpdatesEnabled", newRegistryValue ? 1L : 0L,
                                    RegistryValueKind.QWord);
                            }
                        }

                        LogMessage(String.Format("Set LocalMachine automatic updates flag to {0}",
                            newRegistryValue));
                    } else {
                        // should we enable or disable windows startup
                        if (handle == m_enableWindowsStartupEvent) {
                            using (var regKey = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                                if (regKey != null) {
                                    var clientAppPath = GetClientAppPath();
                                    if (!File.Exists(clientAppPath)) {
                                        LogMessage("Client app could not be found for Windows startup registry entry.");
                                        continue;
                                    }
                                    regKey.SetValue("DesktopBootstrap", clientAppPath);
                                }
                            }
                        } else {
                            using (var regKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                                if (regKey != null) {
                                    regKey.DeleteValue("DesktopBootstrap");
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                LogError("Error listening for automatic update signaling event", e);
            }
        }

        private EventWaitHandle CreateGlobalEventWaitHandle(string name) {
            var users = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            var rule = new EventWaitHandleAccessRule(users, EventWaitHandleRights.Synchronize |
                EventWaitHandleRights.Modify, AccessControlType.Allow);
            var security = new EventWaitHandleSecurity();
            security.AddAccessRule(rule);

            bool createdNew;
            return new EventWaitHandle(false, EventResetMode.AutoReset, name, out createdNew, security);
        }

        #endregion

        #region ClientGuid

        internal static string GetClientGuidAndCreateIfNecessary() {
            try {
                // registry value should have originally been set during first install
                // by the NSIS function InstallClientGuidIfAppropriate.
                using (var regKey = Registry.LocalMachine.CreateSubKey(PermanentRegistryPath)) {
                    var regValue = regKey.GetValue("ClientGuid") as string;

                    if (regValue != null) {
                        try {
                            // make sure regValue parses as a Guid
                            var stringValue = GuidToString(new Guid(regValue));
                            if (GuidStringIsValid(stringValue)) {
                                return stringValue;
                            }
                        } catch (Exception e) {
                            LogError("Exception while trying to read client guid from registry in service", e);
                        }
                    }

                    // something is wrong -- it is missing or malformed
                    // recreate a new one
                    LogMessage(string.Format("Client Guid is missing or malformed ({0}).  Regenerating..", regValue));

                    return GenerateAndSaveNewClientGuid() ?? string.Empty;
                }
            } catch (Exception e) {
                LogError("Exception reading or building ClientGuid from service", e);
                return string.Empty;
            }
        }

        private static string GenerateAndSaveNewClientGuid() {
            try {
                using (var regKey = Registry.LocalMachine.CreateSubKey(PermanentRegistryPath)) {

                    // if there isn't a ClientGuid for some reason then try to create one.
                    // but if we can't do that (e.g. can't write to the registry) then just fallback 
                    // to doing nothing so we don't have clients using new ClientGuids on every run.

                    var tentativeClientGuid = GuidToString(Guid.NewGuid());

                    regKey.SetValue("ClientGuid", tentativeClientGuid);

                    // try to read/verify
                    string readClientGuid = regKey.GetValue("ClientGuid") as string;
                    LogMessage(string.Format("Tentative client guid is {0}.  Read client guid after writing is {1}.",
                        tentativeClientGuid, readClientGuid));

                    if (readClientGuid != tentativeClientGuid) {
                        LogMessage("Writing tentative ClientGuid to registry failed.  Throwing it away and returning zeros.");
                        return GuidToString(Guid.Empty);
                    }

                    LogMessage("Client ClientGuid generation successful.");
                    return tentativeClientGuid;
                }
            } catch (Exception e) {
                LogError("Could not write out new client guid to registry", e);
                return null;
            }
        }

        private static string GuidToString(Guid x) {
            return x.ToString().Replace("-", string.Empty).ToUpperInvariant();
        }

        private static bool GuidStringIsValid(string clientGuid) {
            return clientGuid.Length == 32 && clientGuid != "00000000000000000000000000000000";
        }

        #endregion

        #region Impersonation for relaunching client app after updates

        [DllImport("kernel32.dll")]
        private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(UInt32 sessionId, out IntPtr Token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName,
            string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }
        [Flags]
        private enum CreateProcessFlags {
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SEPARATE_WOW_VDM = 0x00000800,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);
        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hHandle);

        private void RememberClientAppUserSessionIDForUpdate(Process[] clientAppProcesses) {
            try {
                List<string> sessionIDs = new List<string>();
                foreach (var clientAppProcess in clientAppProcesses) {
                    uint sessionID;
                    if (!ProcessIdToSessionId((uint)clientAppProcess.Id, out sessionID)) {
                        // some kind of error
                        return;
                    }

                    LogMessage(String.Format(
                        "Remembering client app session information for relaunch after update.  (Session {0})",
                        sessionID));

                    sessionIDs.Add(sessionID.ToString());
                }

                using (var regKey = Registry.LocalMachine.CreateSubKey(PerInstallationRegistryPath)) {
                    regKey.SetValue("UpdateUserSessionIDs", string.Join(",", sessionIDs.ToArray()), RegistryValueKind.String);
                    regKey.SetValue("UpdateUserSessionIDTimestamp", DateTime.UtcNow.Ticks, RegistryValueKind.QWord);
                }
            } catch (Exception e) {
                LogError("Exception trying to record client app session ID", e);
            }
        }

        private void CheckForAllClientAppUserSessionsFromUpdate() {
            try {
                string[] sessionIDs;
                long sessionIDsTimestamp;
                using (var regKey = Registry.LocalMachine.OpenSubKey(PerInstallationRegistryPath, true)) {
                    if (regKey == null) {
                        return;
                    }

                    sessionIDs = ((regKey.GetValue("UpdateUserSessionIDs") as string) ?? string.Empty).Split(new char[] { ',' },
                        StringSplitOptions.RemoveEmptyEntries);
                    sessionIDsTimestamp = (regKey.GetValue("UpdateUserSessionIDTimestamp") as long?).GetValueOrDefault(0L);

                    regKey.DeleteValue("UpdateUserSessionIDs", false);
                    regKey.DeleteValue("UpdateUserSessionIDTimestamp", false);
                }

                foreach (var sessionID in sessionIDs) {
                    CheckClientAppUserSessionFromUpdate(long.Parse(sessionID), sessionIDsTimestamp);
                }
            } catch (Exception e) {
                LogError("Exception trying to record client app session ID", e);
            }
        }

        private void CheckClientAppUserSessionFromUpdate(long sessionID, long sessionIDTimestamp) {
            bool registryValuesLookGood = true;
            if (sessionID < 0) { // note: session ID 0 can be a valid user interaction session on XP.
                registryValuesLookGood = false;
            }
            if (sessionIDTimestamp <= 0 ||
                    DateTime.UtcNow.Subtract(new DateTime(sessionIDTimestamp)) > TimeSpan.FromMinutes(5)) {

                registryValuesLookGood = false;
            }
            if (!registryValuesLookGood) {
                LogMessage(string.Format(
                    "Not relaunching client app after an update (sessionID = {0}, timestamp = {1}).",
                    sessionID, sessionIDTimestamp));
                return;
            }

            LogMessage("Trying to relaunch client app after an update...");

            // try to find the path to the DesktopBootstrap client app
            var clientAppPath = GetClientAppPath();
            if (!File.Exists(clientAppPath)) {
                LogMessage("Client app could not be found.");
                return;
            }

            // Get the user token.  Note the session could have been closed, etc.
            IntPtr userToken;
            if (!WTSQueryUserToken((uint)sessionID, out userToken)) {
                LogMessage("Query user token failed");
                return;
            }
            try {
                IntPtr environmentBlock;
                if (!CreateEnvironmentBlock(out environmentBlock, userToken, false)) {
                    LogMessage("Creating environment block failed");
                    return;
                }
                try {
                    var startupInfo = new STARTUPINFO();
                    startupInfo.lpDesktop = @"winsta0\default";
                    startupInfo.cb = Marshal.SizeOf(startupInfo);
                    PROCESS_INFORMATION processInfo;
                    if (CreateProcessAsUser(userToken, clientAppPath, null, IntPtr.Zero,
                            IntPtr.Zero, false, (uint)CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT,
                            environmentBlock, Path.GetDirectoryName(clientAppPath),
                            ref startupInfo, out processInfo)) {

                        CloseHandle(processInfo.hProcess);
                        CloseHandle(processInfo.hThread);

                    } else {
                        LogMessage("Creating process as user failed");
                        return;
                    }
                } finally {
                    DestroyEnvironmentBlock(environmentBlock);
                }
            } finally {
                CloseHandle(userToken);
            }

            LogMessage("Client app restarted successfully.");
        }

        #endregion

        #region Update Check Helpers

        private static bool IsServiceDebugFlagSet() {
#if DEBUG
            return true;
#endif
            try {
                using (var regKey = Registry.LocalMachine.OpenSubKey(PermanentRegistryPath, false)) {
                    if (regKey == null) {
                        return false;
                    }
                    return (((regKey.GetValue("IsDebug") as string) ?? string.Empty).ToLowerInvariant() ==
                        "true");
                }
            } catch {
                return false;
            }
        }

        private static bool ShouldAcceptTestCertificate() {
            return IsServiceDebugFlagSet();
        }

        private void BeforeExecutingUpdateFile() {
            // we can't (easily) gracefully shut down the client app since it is running on the user's 
            //   desktop so we can't just PostMessage() to it from Session0.
            // but we will save info about its current state so we can try to re-launch is after the update
            // this will work even for multiple users running the app in different desktop sessions

            RememberClientAppUserSessionIDForUpdate(GetRunningClientAppInstances());
        }

        private static void LogMessage(string logMessage) {
            if (IsServiceDebugFlagSet()) {
                try {
                    var eventSourceName = "DesktopBootstrapService";

                    if (!EventLog.SourceExists(eventSourceName)) {
                        EventLog.CreateEventSource(eventSourceName, "Application");
                    }

                    EventLog.WriteEntry(eventSourceName, logMessage);
                } catch {
                }

                try {
                    OutputDebugString("DesktopBootstrapService: " + logMessage);
                } catch {
                }
            }
        }

        private static void LogExeptionWithLevel(string logMessage, Exception e, string level) {
            LogMessage(string.Join(" -- ", new string[] { 
                logMessage, level, "Message: " + e.Message, "StackTrace: " + e.StackTrace,
                "InnerMessage: " + (e.InnerException == null ? string.Empty : e.InnerException.Message),
                "InnerStackTrace: " + (e.InnerException == null ? string.Empty : e.InnerException.StackTrace)
            }));
        }

        private static void LogWarning(string logMessage, Exception e) {
            LogExeptionWithLevel(logMessage, e, "Warning");
        }

        private static void LogError(string logMessage, Exception e) {
            LogExeptionWithLevel(logMessage, e, "Error");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern void OutputDebugString(string message);

        #endregion

        private static string GetClientAppPath() {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "DesktopBootstrap.exe");
        }

        private Process[] GetRunningClientAppInstances() {
            return Process.GetProcessesByName("DesktopBootstrap");
        }

    }
}
