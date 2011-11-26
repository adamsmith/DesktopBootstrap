[[[Design Goals]]]
* Built for .NET 2.0.
* WinXP and higher
* Everything is 32-bit


[[[Features]]]
* Really great automatic updates
* NSIS installer prints debug/trace messages via OutputDebugString() if debug registry flag is set
* Auto-starts on Windows startup (by default), into a tray icon
* Uses a Windows service that runs as SYSTEM, for auto updates, but can be useful for other purposes like re-registring COM components (grrr COM...)
* User should NEVER see a UAC elevation dialog after the initial install or for an uninstall


[[[Things you must do to use Desktop Boostrap]]]
* Replace all instances of DesktopBootstrap, desktop_bootstrap, etc with your own app's name.
* Delete DesktopBootstrapStrongSignKeys.snk and make your own using 'sn -k FooStrongSignKeys.snk' from client/
* Generate your own update key pair for updates using tools/Keys/self_signed/make_new.py (TODO: check that the p12 update key is double-clickable)
* Customize client/Deployment/Installer/License.rtf


[[[Optional things you should do]]]
* Get your own Authenticode key, put it in the client/Deployment/AuthenticodeKey folder
* Update your application's icon
