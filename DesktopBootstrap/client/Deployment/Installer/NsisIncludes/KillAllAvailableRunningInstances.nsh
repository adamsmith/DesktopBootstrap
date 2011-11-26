
; using the pattern from http://nsis.sourceforge.net/Sharing_functions_between_Installer_and_Uninstaller
!macro KILL_PROC_MACRO un

Function ${un}KillAllAvailableRunningInstances

	; NOTE: a "polite" close isn't easy at all since we can't PostMessage() to DesktopBootstrap.exe
	; since it's running from the user's desktop and we're sometimes running from Session0.  So 
	; we just have to TerminateProcess().
	; 
	; WARNING: these calls only work on 32-bit processes
	; 
	; WARNING: FindProc and KillProc *might* not work if there are multiple users running the
	;   executable.  More specifically, if this code is running from Session0 as SYSTEM then
	;   we'll be able to kill all running instances (e.g. during an update).  If it's running
	;   as a normal user we'll only be able to find and terminate the instances in the current
	;   session/desktop.
	
	Push $R0
	
	FindProcDLL::FindProc "DesktopBootstrap.exe"
	${If} $R0 == 1
		!insertmacro ${un}Debug  "DesktopBootstrap is currently running.  Killing it now..."
		
		killloop:
		KillProcDLL::KillProc "DesktopBootstrap.exe"
		${If} $R0 != 0 ; 0 = process successfully terminated
		${AndIf} $R0 != 603 ; 603 = process was not running.  need this for multi-killproc looping
			!insertmacro ${un}Debug  "Error killing DesktopBootstrap.exe."
		${EndIf}
		
		${If} $R0 == 0
			!insertmacro ${un}Debug  "Sleeping for 8 seconds so DesktopBootstrap.exe is really gone..."
			Sleep 8000
			
			; now loop again in case there's another instance of DesktopBootstrap running
			Goto killloop
		${EndIf}
	${EndIf}
	
	Pop $R0
FunctionEnd

!macroend

!insertmacro KILL_PROC_MACRO ""
!insertmacro KILL_PROC_MACRO "un."