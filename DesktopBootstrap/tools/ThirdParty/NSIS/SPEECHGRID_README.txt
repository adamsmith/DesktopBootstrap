* this is the Unicode fork/build of NSIS.

* there are a couple of additions to Include and Plugins, including FindProcDLL, KillProcDLL (both built for Unicode NSIS), GetProcessInfo, and servicelib.

* I customized GetProcessInfo and servicelib for the Unicode build (mostly making sure System:: calls are passing strings appropriately and not calling the ANSI variants).