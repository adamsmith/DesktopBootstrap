!include "NsisIncludes\GetParent.nsh"

; return value is passed via the stack (call 'Pop $0')
; return without the trailing '\'.
; return "" if not found
; this function clobbers $0
; the returned directory should contain "DesktopBootstrap.exe"
Function FindDesktopBootstrapInstallationFolder
	
	; check the registry 'Run' entry
	; 
	; NOTE THAT THIS WILL FAIL IF THE UPDATER IS BEING RAN FROM THE SERVICE/'SYSTEM' ACCOUNT
	; 
	; It will also fail if the user disabled 'Startup with Windows' through the tray icon.
	${Debug} "Trying to find installation directory via 'Run' registry value..."
	ReadRegStr $0 HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopBootstrap"
	${If} $0 == ""
		${Debug} "Could not find any 'Run' value for DesktopBootstrap"
		Goto check_updater_dir
	${EndIf}
	Push $0
	Call GetParent
	Pop $0
	StrCpy $0 "$0\DesktopBootstrap.exe"
	IfFileExists $0 0 check_updater_dir
	Push $0
	Call GetParent
	Return
	
	check_updater_dir:
	; check the updater's directory
	${Debug} "Trying to find installation directory via looking in our current directory..."
	${GetExePath} $0
	StrCpy $0 "$0\DesktopBootstrap.exe"
	IfFileExists $0 0 check_program_files
	Push $0
	Call GetParent
	Return
	
	check_program_files:
	; check %PROGRAMFILES%
	${Debug} "Trying to find installation directory via looking in %programfiles%..."
	StrCpy $0 "$PROGRAMFILES\DesktopBootstrap\DesktopBootstrap.exe"
	IfFileExists $0 0 fail
	Push $0
	Call GetParent
	Return
	
	fail:
	Push ""
FunctionEnd