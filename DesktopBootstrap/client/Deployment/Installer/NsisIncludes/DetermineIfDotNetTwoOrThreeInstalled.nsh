
!macro DIDNTOTI_FUNC_MACRO un

; warning: clobbers $0 $1 $2 $3 and $4
; 
; usage:
; 
;   Call DetermineIfDotNetTwoOrThreeInstalled
;   Pop $0 ; $0 now contains the latest .NET major version number, or empty string if no .NET is available
Function ${un}DetermineIfDotNetTwoOrThreeInstalled

	StrCpy $3 "0.0.0"
	StrCpy $0 0

	loop:
		; Get each sub key under "SOFTWARE\Microsoft\NET Framework Setup\NDP"
		EnumRegKey $1 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP" $0
 
		StrCmp $1 "" done 	; jump to end if no more registry keys
 
		IntOp $0 $0 + 1 	; Increase registry key index
		StrCpy $4 $1 1 1 	; Looping version number, cut of leading 'v' and take first digit
		
		; we're only interested in 2.x or 3.x, not 4.x
		${If} $4 == "2"
			StrCpy $3 $4
			Goto done
		${ElseIf} $4 == "3"
			StrCpy $3 $4
			Goto done
		${EndIf}
		
		Goto loop

	done:

	; If the latest version is 0.0.0, there is no .NET installed
	${VersionCompare} $3 "0.0.0" $2 ; $2 is output variable
	IntCmp $2 0 no_dotnet finish finish

	no_dotnet:
	StrCpy $3 ""

	finish:

	; $3 contains the latest .NET major version number, or empty string if no .NET is available
	Push $3
FunctionEnd

!macroend

!insertmacro DIDNTOTI_FUNC_MACRO ""
!insertmacro DIDNTOTI_FUNC_MACRO "un."