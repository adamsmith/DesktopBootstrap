!include "NsisIncludes\DetermineIfDotNetTwoOrThreeInstalled.nsh"

; using the pattern from http://nsis.sourceforge.net/Sharing_functions_between_Installer_and_Uninstaller
; so this function can be called during uninstalls, too.
!macro CIP_FUNC_MACRO un

; Typical Usage:
; 
; Call CheckInstallPrereqs
; Pop $0  ; long, readable error message, or "ok" if no error
; Pop $1  ; short, machine_like_error_message, or "ok" if there was no error
; ${If} $0 != "ok"
;		MessageBox MB_OK|MB_ICONINFORMATION $0
;		Quit
; ${EndIf}
; 
; NOTE: clobbers $0 thru $7
Function ${un}CheckInstallPrereqs

	; Prerequisites --
	;   DesktopBootstrap can't be already installed or have been installed.
	;   Running as admin
	;   Windows XP or higher
	;   Is a .NET CLR installed that can run .NET 2.0 apps?
  
	
	; Are we already installed? / Have we been installed?
	${DirState} "$LOCALAPPDATA\DesktopBootstrap" $0
	${DirState} "$PROGRAMFILES\DesktopBootstrap" $1
	${If} "$0" != "-1" ; -1 means the directory was not found
	${OrIf} "$1" != "-1"
		Push "already_installed_directory"
		Push "DesktopBootstrap is already installed!  Setup will now exit."
		Return
	${EndIf}
	${If} $client_guid_already_existed != "no"
		Push "already_installed_registry"
		Push "DesktopBootstrap has already been installed in the past.  Setup will now exit."
		Return
	${EndIf}
	
	; Running as admin?
	UserInfo::GetAccountType
	Pop $0
	${If} $0 != "Admin"
		Push "not_admin_user"
		Push "DesktopBootstrap setup must be ran under an account with Administrator privileges!  Setup will now exit."
		Return
	${EndIf}
	
	; Are we running an okay version of Windows?
	${IfNot} ${AtLeastWinXP}
		Push "incompatible_windows_version"
		Push "DesktopBootstrap did not detect a compatible version of Microsoft Windows.  Setup will now exit."
		Return
	${EndIf}
	
	; Check for .NET 2.0 Framework
	${If} ${IsWinXP} ; Vista and higher will always have the 2.0 Framework.
	  ; On Windows XP we'll need .NET 2.0, 3.0, or 3.5 installed.  .NET 4.0 isn't helpful, I think.
	  Call ${un}DetermineIfDotNetTwoOrThreeInstalled
	  Pop $0
		${If} $0 == ""
			Push "incompatible_dotnet_version"
			Push "DesktopBootstrap requires the Microsoft .NET Framework 2.0 or higher.  Please email support@DesktopBootstrap.com if you have any questions or concerns.  Setup will now exit." 
			Return
		${EndIf}
	${EndIf}
  
  Push "ok"
  Push "ok"
  
FunctionEnd

!macroend

!insertmacro CIP_FUNC_MACRO ""
!insertmacro CIP_FUNC_MACRO "un."