using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Resources;
using System.Runtime.InteropServices;
using System.Reflection;

namespace DesktopBootstrap {

    public partial class TrayIconForm : Form {

        private static List<TrayIconForm> s_openInstances = new List<TrayIconForm>();

        public TrayIconForm() {
            InitializeComponent();
            Visible = false;
            s_openInstances.Add(this);

            CleanUpOldTrayIconIfAppropriate();
            SaveReferenceToNewTrayIcon();
            
            RefreshDisplayTextForDynamicMenuItems();
        }

        private void RefreshDisplayTextForDynamicMenuItems() {
            if (InvokeRequired) {
                BeginInvoke(new SimpleDelegate(RefreshDisplayTextForDynamicMenuItems));
                return;
            }

            if (LibraryRegistry.GetBooleanValue(RegSettingNames.AutomaticUpdatesEnabled).GetValueOrDefault(true)) {
                toggleAutomaticUpdatesToolStripMenuItem.Text = "Disable automatic updates";
            } else {
                toggleAutomaticUpdatesToolStripMenuItem.Text = "Enable automatic updates";
            }

            if (LibraryRegistry.GetBooleanValue(RegSettingNames.StartupWithWindowsEnabled).GetValueOrDefault(true)) {
                toggleStartupWithWindowsToolStripMenuItem.Text = "Disable startup with Windows";
            } else {
                toggleStartupWithWindowsToolStripMenuItem.Text = "Enable startup with Windows";
            }

            trayIcon.Text = "DesktopBootstrap";
        }

        private void TrayIconForm_FormClosing(object sender, FormClosingEventArgs e) {
            s_openInstances.Remove(this);
            DeleteReferenceToProperlyClosedTrayIcon();
        }

        private void trayIcon_MouseClick(object sender, MouseEventArgs e) {
            // the context menu won't show by default on left clicks.  we're going to have to ask it to show up.
            if (e.Button == MouseButtons.Left) {
                try {
                    // try using reflection to get to the private ShowContextMenu() function...which really 
                    // should be public but is not.
                    var showContextMenuMethod = trayIcon.GetType().GetMethod("ShowContextMenu",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    showContextMenuMethod.Invoke(trayIcon, null);
                } catch (Exception exception) {
                    // something went wrong with out hack -- fall back to a shittier approach
                    Log.Logger.WarnException("ShowContextMenu reflection failed", exception);
                    trayMenu.Show(Cursor.Position);
                }
            }
        }

        private void quitDesktopBootstrapToolStripMenuItem_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void toggleStartWithWindowsToolStripMenuItem_Click(object sender, EventArgs e) {
            LibraryRegistry.SetValue(RegSettingNames.StartupWithWindowsEnabled, !LibraryRegistry.GetBooleanValue(
                RegSettingNames.StartupWithWindowsEnabled).GetValueOrDefault(true));

            RefreshDisplayTextForDynamicMenuItems();

            if (LibraryRegistry.GetBooleanValue(RegSettingNames.StartupWithWindowsEnabled).GetValueOrDefault(true)) {
                // add the Windows 'Run' registry value back in
                ServiceIpc.SignalEnableWindowsStartup();
                
                Log.Logger.Debug("User reenabled startup with Windows.");
            } else {
                // delete the Windows 'Run' registry value
                ServiceIpc.SignalDisableWindowsStartup();

                Log.Logger.Debug("User disabled startup with Windows.");
            }
        }

        private void toggleAutomaticUpdatesToolStripMenuItem_Click(object sender, EventArgs e) {
            // we have three things to do:
            // 
            // first, update the flag in our currentuser settings
            // second, update the display text for the menu item
            // third, update the flag in localmachine, by signaling to the service
            
            LibraryRegistry.SetValue(RegSettingNames.AutomaticUpdatesEnabled, !LibraryRegistry.GetBooleanValue(
                RegSettingNames.AutomaticUpdatesEnabled).GetValueOrDefault(true));

            RefreshDisplayTextForDynamicMenuItems();


            if (LibraryRegistry.GetBooleanValue(RegSettingNames.AutomaticUpdatesEnabled).GetValueOrDefault(true)) {
                ServiceIpc.SignalEnableAutoUpdates();
                Log.Logger.Debug("User reenabled automatic updates.");
            } else {
                // updates were enabled, but have now been disabled
                ServiceIpc.SignalEnableAutoUpdates();
                Log.Logger.Debug("User disabled automatic updates.");
            }
        }

        internal static void HideForThirtySeconds() {
            foreach (var openInstance in s_openInstances) {
                Log.Logger.Debug("Disabling tray icon");

                openInstance.hideTimer.Interval = 30000;
                openInstance.hideTimer.Enabled = true;
                openInstance.trayIcon.Visible = false;
            }
        }

        private void hideTimer_Tick(object sender, EventArgs e) {
            Log.Logger.Debug("Reenabling tray icon");
            hideTimer.Enabled = false;
            trayIcon.Visible = true;
        }

        private void TrayIconForm_VisibleChanged(object sender, EventArgs e) {
            // Application.Run(Form) changes this form to be visible.  Change it back.
            Visible = false;
        }

        #region Removing dead DesktopBootstrap tray icons

        [DllImport("shell32.dll")]
        private static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NOTIFYICONDATA pnid);
        private struct NOTIFYICONDATA {
            public int cbSize;
            public IntPtr hwnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }
        private const int NIM_DELETE = 0x00000002;

        private void CleanUpOldTrayIconIfAppropriate() {
            // If we closed improperly last time, make sure the old tray icon isn't still around.
            try {
                var oldTrayIconID = LibraryRegistry.GetInt32Value(RegSettingNames.LastTrayID);
                var oldTrayIconHwnd = LibraryRegistry.GetInt32Value(RegSettingNames.LastTrayHwnd);
                if (oldTrayIconID.HasValue && oldTrayIconHwnd.HasValue) {
                    try {
                        var oldTrayIconData = new NOTIFYICONDATA();
                        oldTrayIconData.hwnd = new IntPtr(oldTrayIconHwnd.Value);
                        oldTrayIconData.uID = oldTrayIconID.Value;

                        Shell_NotifyIcon(NIM_DELETE, ref oldTrayIconData);
                    } finally {
                        LibraryRegistry.DeleteValue(RegSettingNames.LastTrayID);
                        LibraryRegistry.DeleteValue(RegSettingNames.LastTrayHwnd);
                    }
                }
            } catch (Exception e) {
                Log.Logger.WarnException("Exception removing last tray icon", e);
            }
        }

        private void SaveReferenceToNewTrayIcon() {
            // Save our new tray icon info in case we get killed and restart, so that we'll be able
            // to clean up the tray.  (E.g. after an update, the new version will be able to clean
            // up the icon from before the update.)
            try {
                int trayIconID = (int)trayIcon.GetType().GetField("id", BindingFlags.NonPublic |
                    BindingFlags.Instance).GetValue(trayIcon);

                var window = (NativeWindow)trayIcon.GetType().GetField("window", BindingFlags.NonPublic |
                    BindingFlags.Instance).GetValue(trayIcon);

                if (window != null && window.Handle != IntPtr.Zero) {
                    LibraryRegistry.SetInt32Value(RegSettingNames.LastTrayID, trayIconID);
                    LibraryRegistry.SetInt32Value(RegSettingNames.LastTrayHwnd, window.Handle.ToInt32());
                }
            } catch (Exception e) {
                Log.Logger.ErrorException("Exception updating last tray icon data", e);
            }
        }

        private void DeleteReferenceToProperlyClosedTrayIcon() {
            // we're shutting down properly, so no need to save the last tray icon info
            LibraryRegistry.DeleteValue(RegSettingNames.LastTrayID);
            LibraryRegistry.DeleteValue(RegSettingNames.LastTrayHwnd);
        }

        #endregion

    }

}
