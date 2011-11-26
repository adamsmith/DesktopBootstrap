using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace DesktopBootstrap {

    internal static class RegSettingNames {
        internal static readonly string LastTrayID = "LastTrayID";
        internal static readonly string LastTrayHwnd = "LastTrayHwnd";
        internal static readonly string AutomaticUpdatesEnabled = "AutomaticUpdatesEnabled"; // don't change this value.  used by service.
        internal static readonly string StartupWithWindowsEnabled = "StartupWithWindowsEnabled";
    }

    internal static class LibraryRegistry {

        // all of this is in HKCU
        internal static readonly string PermanentRegistryPath = @"Software\DesktopBootstrap";
        internal static readonly string PerInstallationRegistryPath = @"Software\DesktopBootstrap\AppData";

        #region Get Values

        internal static DateTime? GetDateTimeValue(string valueName) {
            var longVal = GetInt64Value(valueName);
            if (longVal.HasValue) {
                return new DateTime(longVal.Value);
            }
            return null;
        }

        internal static TimeSpan? GetTimeSpanValue(string valueName) {
            return GetTimeSpanValue(Registry.CurrentUser, valueName);
        }

        internal static TimeSpan? GetTimeSpanValue(RegistryKey regHiveKey, string valueName) {
            var longVal = GetInt64Value(regHiveKey, valueName);
            if (longVal.HasValue) {
                return new TimeSpan(longVal.Value);
            }
            return null;
        }

        internal static long? GetInt64Value(string valueName) {
            return GetInt64Value(Registry.CurrentUser, valueName);
        }

        internal static long? GetInt64Value(RegistryKey regHiveKey, string valueName) {
            return GetObjectValue(regHiveKey, PerInstallationRegistryPath, valueName) as long?;
        }

        internal static int? GetInt32Value(string valueName) {
            return GetInt32Value(Registry.CurrentUser, valueName);
        }

        internal static int? GetInt32Value(RegistryKey regHiveKey, string valueName) {
            return GetObjectValue(regHiveKey, PerInstallationRegistryPath, valueName) as int?;
        }

        internal static bool? GetBooleanValue(string valueName) {
            var longVal = GetInt64Value(valueName);
            if (longVal.HasValue) {
                return (longVal != 0);
            }
            return null;
        }

        internal static string GetStringValue(string valueName) {
            return GetStringValue(PerInstallationRegistryPath, valueName);
        }

        internal static string GetStringValue(string regKeyPath, string valueName) {
            return GetObjectValue(Registry.CurrentUser, regKeyPath, valueName) as string;
        }

        private static object GetObjectValue(RegistryKey regHiveKey, string regKeyPath, string valueName) {
            try {
                using (var DesktopBootstrapRegKey = regHiveKey.OpenSubKey(regKeyPath, false)) {
                    if (DesktopBootstrapRegKey == null) {
                        return null;
                    }
                    return DesktopBootstrapRegKey.GetValue(valueName);
                }
            } catch (Exception e) {
                Log.Logger.WarnException("Exception getting registry value", e);
                return null;
            }
        }

        #endregion

        #region Set Values

        internal static bool SetValue(string valueName, DateTime value) {
            return SetInt64Value(valueName, value.Ticks);
        }

        internal static bool SetValue(string valueName, TimeSpan value) {
            return SetInt64Value(valueName, value.Ticks);
        }

        internal static bool SetValue(string valueName, bool value) {
            return SetInt64Value(valueName, value ? 1L : 0L);
        }

        internal static bool SetValue(string valueName, string value) {
            return SetValue(PerInstallationRegistryPath, valueName, value);
        }

        internal static bool SetValue(string regKeyPath, string valueName, string value) {
            return SetObjectValue(regKeyPath, valueName, value, RegistryValueKind.String);
        }

        internal static bool SetInt32Value(string valueName, int value) {
            return SetObjectValue(PerInstallationRegistryPath, valueName, value, RegistryValueKind.DWord);
        }

        internal static bool SetInt64Value(string valueName, long value) {
            return SetObjectValue(PerInstallationRegistryPath, valueName, value, RegistryValueKind.QWord);
        }

        private static bool SetObjectValue(string regKeyPath, string valueName, object value, RegistryValueKind valueKind) {
            try {
                using (var DesktopBootstrapRegKey = Registry.CurrentUser.CreateSubKey(regKeyPath)) {
                    if (DesktopBootstrapRegKey == null) {
                        return false;
                    }
                    DesktopBootstrapRegKey.SetValue(valueName, value, valueKind);
                    return true;
                }
            } catch (Exception e) {
                Log.Logger.WarnException("Exception getting registry value", e);
                return false;
            }
        }

        #endregion

        #region Delete Value

        internal static void DeleteValue(string valueName) {
            DeleteValue(PerInstallationRegistryPath, valueName);
        }

        internal static void DeleteValue(string regKeyPath, string valueName) {
            try {
                using (var DesktopBootstrapRegKey = Registry.CurrentUser.OpenSubKey(regKeyPath, true)) {
                    if (DesktopBootstrapRegKey == null) {
                        return;
                    }
                    DesktopBootstrapRegKey.DeleteValue(valueName, false);
                }
            } catch (Exception e) {
                Log.Logger.WarnException("Exception deleting registry value", e);
            }
        }

        #endregion

        #region ClientGuid

        // This logic gets hairy because the registry is a "noisy" component to deal with

        private static string k_clientGuid = null;
        private static object k_clientGuidSyncRoot = new object();

        internal static string ClientGuid {
            get {
                lock (k_clientGuidSyncRoot) {
                    if (k_clientGuid == null) {
                        try {
                            // registry value should have originally been set during first install
                            // by the NSIS function InstallClientGuidIfAppropriate.

                            k_clientGuid = ReadClientGuid(Registry.CurrentUser);

                            if (k_clientGuid == null) {
                                k_clientGuid = ReadClientGuid(Registry.LocalMachine);

                                if (k_clientGuid == null) {
                                    // there isn't a valid client guid in HKLM either
                                    // this shouldn't happen, because the service checks when it starts up
                                    //   to verify that there is one
                                    // there's not much we can do here
                                    // return all zeros
                                    k_clientGuid = GuidToString(Guid.Empty);
                                } else {
                                    // write the HKLM ClientGuid to HKCU
                                    WriteClientGuidToHkcu(k_clientGuid);
                                }
                            }
                        } catch (Exception e) {
                            Log.Logger.ErrorException("Exception reading or copying ClientGuid", e);
                            k_clientGuid = GuidToString(Guid.Empty);
                        }
                    }

                    return k_clientGuid;
                }
            }
        }

        private static void WriteClientGuidToHkcu(string clientGuid) {
            if (!GuidStringIsValid(clientGuid)) {
                Log.Logger.Error("Guid string cannot be written to registry.  It is invalid.", new OtherInfo("ClientGuidAttempted", clientGuid));
            }

            try {
                using (var regKey = Registry.CurrentUser.CreateSubKey(PermanentRegistryPath)) {
                    regKey.SetValue("ClientGuid", clientGuid);
                }
            } catch (Exception e) {
                Log.Logger.ErrorException("Exception writing copied client guid to per user registry", e);
            }
        }

        private static string ReadClientGuid(RegistryKey regHive) {
            try {
                using (var regKey = regHive.OpenSubKey(PermanentRegistryPath)) {
                    if (regKey == null) {
                        return null;
                    }

                    var regValue = regKey.GetValue("ClientGuid") as string;
                    if (regValue == null) {
                        return null;
                    }

                    // make sure regValue parses as a Guid
                    var stringValue = GuidToString(new Guid(regValue));

                    if (!GuidStringIsValid(stringValue)) {
                        return null;
                    }

                    return stringValue;
                }
            } catch (Exception e) {
                Log.Logger.WarnException("Exception while trying to read client guid from registry", e);
                return null;
            }
        }

        internal static string GuidToString(Guid x) {
            return x.ToString().Replace("-", string.Empty).ToUpperInvariant();
        }

        private static bool GuidStringIsValid(string clientGuid) {
            return clientGuid.Length == 32 && clientGuid != "00000000000000000000000000000000";
        }

        #endregion

    }
}
