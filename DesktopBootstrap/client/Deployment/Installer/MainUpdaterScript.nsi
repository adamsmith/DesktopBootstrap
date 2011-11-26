Name "DesktopBootstrap Updater"
VIProductVersion "${VERSION}"
VIAddVersionKey "FileDescription" "DesktopBootstrap Updater"
VIAddVersionKey "ProductName" "DesktopBootstrap Updater"
VIAddVersionKey "FileVersion" "${VERSION}"
OutFile "current_build_bin\out\DesktopBootstrapUpdater.exe"
Icon "..\..\..\tools\Artwork\icon\app.ico"
SetCompressor /SOLID lzma
RequestExecutionLevel admin
SilentInstall silent

Var executable_type ; e.g. "installer" "uninstaller" "updater"
Var client_guid_already_existed

!include "LogicLib.nsh"
!include "WordFunc.nsh"
!include "StrFunc.nsh"
${StrLoc} ; must initialize this before it can be used in a Function (a nuance of StrFunc.nsh)
${UnStrLoc}
${StrRep}
${UnStrRep}
!include "FileFunc.nsh"
!include "WinVer.nsh"
!include "GetProcessInfo.nsh"
!include "servicelib.nsh"
!include "NsisIncludes\Debug.nsh"
!include "NsisIncludes\CheckAlreadyRunningInstallOrUninstall.nsh"
!include "NsisIncludes\FindDesktopBootstrapInstallationFolder.nsh"
!include "NsisIncludes\KillAllAvailableRunningInstances.nsh"

!define OutputDebugString `System::Call kernel32::OutputDebugString(ts)`

Section ""
	StrCpy $executable_type "updater"
	
	Call SilentCheckAlreadyRunningInstallOrUninstall
	Pop $0
	${If} $0 != 0
		${Debug} "Installer/uninstaller/updater is already running.  Quiting."
		Quit
	${EndIf}
	
	Call FindDesktopBootstrapInstallationFolder
	Pop $0
	${If} $0 == ""
		${Debug} "Could not find installation folder.  Quiting."
		Quit
	${EndIf}
	${Debug} "Installation found at $0"
	
	; === DO NOT CLOBBER $0 AFTER THIS POINT ===
	
	; Compare versions to make sure the version in this updater is newer than the preexisting one
	${Debug} "Checking versions..."
	GetTempFileName $1 "$0\"
	File "/oname=$1" "current_build_bin\in-obfuscated\DesktopBootstrap.exe"
	${GetFileVersion} "$1" $2
	Delete $1
	${If} $2 == ""
		${Debug} "Could not read embedded DesktopBootstrap.exe version.  Quiting.  (DesktopBootstrap.exe was not killed.)"
		Quit
	${EndIf}
	${GetFileVersion} "$0\DesktopBootstrap.exe" $3
	${If} $2 == ""
		${Debug} "Could not read existing DesktopBootstrap.exe version -- continuing..."
		Goto done_version_compare
	${EndIf}
	${VersionCompare} $2 $3 $4
	${If} $4 != 1
		${Debug} "The existing version is either equal or greater ($2 $3 $4).  Quiting.  (DesktopBootstrap.exe was not killed.)"
		Quit
	${EndIf}
	done_version_compare:
	${Debug} "Versions check out.  We are newer.  Continuing..."
	
	; === DO NOT CLOBBER $0 AFTER THIS POINT ===
	
	; Kill DesktopBootstrap.exe if it's running
	Call KillAllAvailableRunningInstances
  
	; === DO NOT CLOBBER $0 AFTER THIS POINT ===
	
	; Try to stop service, as well, so we can update it
	${Debug} "Stopping service..."
  !insertmacro SERVICE "stop" "DesktopBootstrapService" ""
  Sleep 2000
  
	; === DO NOT CLOBBER $0 AFTER THIS POINT ===
	
	; Copy files
	${Debug} "Copying files..."
	SetOutPath "$0\"
	SetOverwrite try
	ClearErrors
	File "current_build_bin\in-obfuscated\DesktopBootstrap.exe"
	IfErrors 0 no_file_copy_error
		${Debug} "Error encountered when trying to replace DesktopBootstrap.exe.  Relaunching service, then quiting.  Service might restart DesktopBootstrap.exe."
		
  	!insertmacro SERVICE "start" "DesktopBootstrapService" ""
		
		Quit
	no_file_copy_error:
	File "current_build_bin\in\DesktopBootstrap.exe.config"
	File "current_build_bin\in\NLog.dll"
	
	File "current_build_bin\in-obfuscated\DesktopBootstrapService.exe"
	File "current_build_bin\in\DesktopBootstrapService.exe.config"
	
	File "current_build_bin\out\Uninstaller.exe"
	
	; Start new service
	${Debug} "Starting DesktopBootstrapService (which might start DesktopBootstrap.exe)..."
  !insertmacro SERVICE "start" "DesktopBootstrapService" ""
  
  ; Don't restart DesktopBootstrap.exe.  It will be restarted from the service using
  ; impersonation.
	
	${Debug} "Done with update.  Quiting."
SectionEnd