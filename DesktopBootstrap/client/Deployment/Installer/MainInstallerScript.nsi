Name "DesktopBootstrap"
VIProductVersion "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
!ifdef WRITE_UNINSTALLER_ONLY
	VIAddVersionKey "FileDescription" "DesktopBootstrap Uninstaller"
	VIAddVersionKey "ProductName" "DesktopBootstrap Uninstaller"
	OutFile "current_build_bin\out\DesktopBootstrapUninstallerGenerator.exe"
!else
	VIAddVersionKey "FileDescription" "DesktopBootstrap Installer"
	VIAddVersionKey "ProductName" "DesktopBootstrap Installer"
	OutFile "current_build_bin\out\DesktopBootstrapInstaller.exe"
!endif
Icon "..\..\..\tools\Artwork\icon\app.ico"
SetCompressor /SOLID lzma
RequestExecutionLevel admin
InstallDir "$PROGRAMFILES\DesktopBootstrap"
BrandingText " "
ShowInstDetails nevershow
ShowUninstDetails nevershow

Var executable_type ; e.g. "installer" "uninstaller" "updater" etc

!include "MUI.nsh"
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
!include "NsisIncludes\GenerateClientGuidIfAppropriate.nsh"
!include "NsisIncludes\CheckInstallPrereqs.nsh"
!include "NsisIncludes\CheckAlreadyRunningInstallOrUninstall.nsh"
!include "NsisIncludes\KillAllAvailableRunningInstances.nsh"

!define MUI_ICON "..\..\..\tools\Artwork\icon\app.ico"
!define MUI_UNICON "..\..\..\tools\Artwork\icon\app.ico"
!insertmacro MUI_PAGE_LICENSE "License.rtf"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_INSTFILES
!define MUI_CUSTOMFUNCTION_ABORT UserAbortedInstallCallback

!verbose push
!verbose 3
!insertmacro MUI_LANGUAGE "Afrikaans"
!insertmacro MUI_LANGUAGE "Albanian"
!insertmacro MUI_LANGUAGE "Arabic"
!insertmacro MUI_LANGUAGE "Basque"
!insertmacro MUI_LANGUAGE "Belarusian"
!insertmacro MUI_LANGUAGE "Bosnian"
!insertmacro MUI_LANGUAGE "Breton"
!insertmacro MUI_LANGUAGE "Bulgarian"
!insertmacro MUI_LANGUAGE "Catalan"
!insertmacro MUI_LANGUAGE "Cibemba"
!insertmacro MUI_LANGUAGE "Croatian"
!insertmacro MUI_LANGUAGE "Czech"
!insertmacro MUI_LANGUAGE "Danish"
!insertmacro MUI_LANGUAGE "Dutch"
!insertmacro MUI_LANGUAGE "Efik"
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Esperanto"
!insertmacro MUI_LANGUAGE "Estonian"
!insertmacro MUI_LANGUAGE "Farsi"
!insertmacro MUI_LANGUAGE "Finnish"
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "Galician"
!insertmacro MUI_LANGUAGE "Georgian"
!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "Greek"
!insertmacro MUI_LANGUAGE "Hebrew"
!insertmacro MUI_LANGUAGE "Hungarian"
!insertmacro MUI_LANGUAGE "Icelandic"
!insertmacro MUI_LANGUAGE "Igbo"
!insertmacro MUI_LANGUAGE "Indonesian"
!insertmacro MUI_LANGUAGE "Irish"
!insertmacro MUI_LANGUAGE "Italian"
!insertmacro MUI_LANGUAGE "Japanese"
!insertmacro MUI_LANGUAGE "Khmer"
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "Kurdish"
!insertmacro MUI_LANGUAGE "Latvian"
!insertmacro MUI_LANGUAGE "Lithuanian"
!insertmacro MUI_LANGUAGE "Luxembourgish"
!insertmacro MUI_LANGUAGE "Macedonian"
!insertmacro MUI_LANGUAGE "Malagasy"
!insertmacro MUI_LANGUAGE "Malay"
!insertmacro MUI_LANGUAGE "Mongolian"
!insertmacro MUI_LANGUAGE "Norwegian"
!insertmacro MUI_LANGUAGE "NorwegianNynorsk"
!insertmacro MUI_LANGUAGE "Pashto"
!insertmacro MUI_LANGUAGE "Polish"
!insertmacro MUI_LANGUAGE "Portuguese"
!insertmacro MUI_LANGUAGE "PortugueseBR"
!insertmacro MUI_LANGUAGE "Romanian"
!insertmacro MUI_LANGUAGE "Russian"
!insertmacro MUI_LANGUAGE "Serbian"
!insertmacro MUI_LANGUAGE "SerbianLatin"
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "Slovak"
!insertmacro MUI_LANGUAGE "Slovenian"
!insertmacro MUI_LANGUAGE "Spanish"
!insertmacro MUI_LANGUAGE "SpanishInternational"
!insertmacro MUI_LANGUAGE "Swahili"
!insertmacro MUI_LANGUAGE "Swedish"
!insertmacro MUI_LANGUAGE "Thai"
!insertmacro MUI_LANGUAGE "TradChinese"
!insertmacro MUI_LANGUAGE "Turkish"
!insertmacro MUI_LANGUAGE "Ukrainian"
!insertmacro MUI_LANGUAGE "Uzbek"
;!insertmacro MUI_LANGUAGE "Valencian" ; commented out b/c it caused a build error
!insertmacro MUI_LANGUAGE "Vietnamese"
!insertmacro MUI_LANGUAGE "Welsh"
;!insertmacro MUI_LANGUAGE "Yoruba" ; commented out b/c it caused a build error
!verbose pop

Function UserAbortedInstallCallback
	${Debug} "User aborted install"
FunctionEnd

Function un.onInit
	StrCpy $executable_type "uninstaller"
	Call un.CheckAlreadyRunningInstallOrUninstall
FunctionEnd

Function .onInit
	!ifdef WRITE_UNINSTALLER_ONLY
		; this is a build of the setup that is only meant to emit the uninstaller (so we can subsequently sign it)
		System::Call "kernel32::GetCurrentDirectoryW(i ${NSIS_MAX_STRLEN}, t .r0)"
		WriteUninstaller "$0\Uninstaller.exe"
		SetErrorLevel 0
		Quit
	!endif
	
	${StrLoc} $0 $CMDLINE "testprereqsonly" ">"
	${If} $0 != ""
		${Debug} "testprereqsonly command arg set; testing prereqs only.."
		
		Call CheckInstallPrereqs
		Pop $0
		Pop $1
		${If} $0 == "ok"
			${Debug} "prereqs checked out ok"
			SetErrorLevel 13 ; just pick some rare values
		${Else}
			${Debug} "prereqs checked failed"
			SetErrorLevel 14
		${EndIf}
		
		Quit
	${EndIf}
	
	StrCpy $executable_type "installer"
	
	Call CheckAlreadyRunningInstallOrUninstall
	
	Call GenerateClientGuidIfAppropriate

	; Check for installation prereq's
	${StrLoc} $0 $CMDLINE "skipprereqs" ">"
	${If} $0 == "" ; no match
		Call CheckInstallPrereqs
		Pop $0
		Pop $1
		${If} $0 != "ok"
			${Debug} "Prereq fail reason: $1"
			
			MessageBox MB_OK|MB_ICONINFORMATION $0
			SetErrorLevel 21
			Quit
		${EndIf}
	${Else}
		${Debug} "Skipping prereqs check due to command line argument..."
	${EndIf}
FunctionEnd

Section ""
!ifndef WRITE_UNINSTALLER_ONLY ; otherwise don't include an installer section

	; Do this especially before launching DesktopBootstrap or any of the executables.
	Call WriteTentativeOrActualClientGuidToRegistry

	; Let the fun begin!
	${Debug} "Copying files..."
	SetOutPath "$INSTDIR"
	File "current_build_bin\in-obfuscated\DesktopBootstrap.exe"
	File "current_build_bin\in\DesktopBootstrap.exe.config"
	File "current_build_bin\in\NLog.dll"
	
	File "current_build_bin\in-obfuscated\DesktopBootstrapService.exe"
	File "current_build_bin\in\DesktopBootstrapService.exe.config"

	; Set 'Run' key in registry
	; 
	; Note: if changing this to add command line args, extra quotes, etc., check to see if you need to
	; also update the 'FindDesktopBootstrapInstallationFolder' function in the Updater.  If so, make sure to
	; retain backwards compatibility in the updater as well.
	; 
	; Note 2: remember that the user can manually enable/disable this in the app, which will manipulate
	; this registry value.
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopBootstrap" "$INSTDIR\DesktopBootstrap.exe"
	
	; Add 'Program Files' shortcut.  This is particularly (well, somewhat) important for users who disable
	; auto-start.
	SetShellVarContext all ; install shortcut for all users
	CreateShortCut "$SMPROGRAMS\DesktopBootstrap.lnk" "$INSTDIR\DesktopBootstrap.exe"
	
	; Install service
  ; don't forget the trailing ';' in the param list
  !insertmacro SERVICE "create" "DesktopBootstrapService" "path=$INSTDIR\DesktopBootstrapService.exe;autostart=1;interact=0;display=DesktopBootstrapService;description=DesktopBootstrap Service maintains your installation of DesktopBootstrap to ensure it is always up to date.;"
  
	; Setup uninstaller
	File "current_build_bin\out\Uninstaller.exe"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopBootstrap" "DisplayName" "DesktopBootstrap"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopBootstrap" "DisplayIcon" "$\"$INSTDIR\DesktopBootstrap.exe$\""
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopBootstrap" "UninstallString" "$\"$INSTDIR\Uninstaller.exe$\""
	
	; Launch app & service
	Exec "$INSTDIR\DesktopBootstrap.exe"
  !insertmacro SERVICE "start" "DesktopBootstrapService" ""
  
	${Debug} "Install completed."
	
!endif
SectionEnd

Section "Uninstall"
	; kill all possible running instances
	Call un.KillAllAvailableRunningInstances

	; remove old tray icon if appropriate / possible
	ReadRegDWORD $0 HKCU "Software\DesktopBootstrap\AppData" "LastTrayHwnd"
	ReadRegDWORD $1 HKCU "Software\DesktopBootstrap\AppData" "LastTrayID"
	${If} $0 > 0
		System::Call '*(&l4, i, i, i, i, i, &t64) i(, $0, $1, 0, 0, 0, "") .r0'
		System::Call 'Shell32::Shell_NotifyIcon(i 2, i r0) i.r1'
		System::Free $0
	${EndIf}
	
	; Note that DesktopBootstrap.exe could still be running in other user sessions
	; Thus we'll specify /REBOOTOK when deleting files, and let the user know if they need to reboot
	
	; stop and uninstall service
  !insertmacro SERVICE "stop" "DesktopBootstrapService" ""
  Sleep 2000
  !insertmacro SERVICE "delete" "DesktopBootstrapService" ""
  
  RMDir /r /REBOOTOK "$INSTDIR"  ; This will delete all of the files, including the uninstaller.
  RMDir /r /REBOOTOK "$LOCALAPPDATA\DesktopBootstrap"  ; Log files, etc.  We might leave behind ones for other users.
  
  ; delete the 'Program Files' shortcut
	SetShellVarContext all ; uninstall shortcut for all users
	Delete /REBOOTOK "$SMPROGRAMS\DesktopBootstrap.lnk"
  
  ; there might be other AppData's for other users, but we'll unfortunately have
  ;   to leave those behind.
  DeleteRegKey HKCU "Software\DesktopBootstrap\AppData" ; Don't delete the ClientGuid
  DeleteRegKey HKLM "Software\DesktopBootstrap\AppData" ; Don't delete the ClientGuid
  
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopBootstrap"
  DeleteRegValue HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopBootstrap"
	
	IfRebootFlag 0 noreboot
		MessageBox MB_YESNO|MB_ICONINFORMATION "There are some files that will not be deleted until you reboot your computer, probably because another user is running DesktopBootstrap.  Would you like to reboot now?" IDNO noreboot
		Reboot
	noreboot:
	
	${un.Debug} "Uninstall completed."
SectionEnd