Var client_guid_already_existed
Var tentative_or_actual_client_guid

; (This function will write to the registry under certain conditions.)
Function GenerateClientGuidIfAppropriate
	; Try reading an existing Client Guid, if there is one
	ReadRegStr $0 HKCU "Software\DesktopBootstrap" "ClientGuid"
	${If} $0 == ""
		${Debug} "No HKCU ClientGuid found"
		ReadRegStr $0 HKLM "Software\DesktopBootstrap" "ClientGuid"
		${If} $0 != ""
			${Debug} "HKLM ClientGuid found.  Copying it to HKCU."
			; there was a clientguid that was likely installed by a different user
			; note that we're in x86 mode here so the HKLM registry key is redirected
			WriteRegStr HKCU "Software\DesktopBootstrap" "ClientGuid" "$0"
		${EndIf}
	${EndIf}
	${If} $0 == ""
		${Debug} "Generating new tentative ClientGuid..."
		StrCpy $client_guid_already_existed "no"
		
		; Generate and write a new value
		Call GenerateGuid
		Pop $0
		StrCpy $tentative_or_actual_client_guid $0
	${Else}
		${Debug} "ClientGuid already existed"
		StrCpy $client_guid_already_existed "yes"
		
		StrCpy $tentative_or_actual_client_guid $0
	${EndIf}
FunctionEnd


; Note: Must call GenerateClientGuidIfAppropriate before calling this function!
Function WriteTentativeOrActualClientGuidToRegistry
	${Debug} "Writing tentative (or actual) client guid to the registry..."
	
	${If} $tentative_or_actual_client_guid == ""
		; I can't think of any way that this might happen.
		${Debug} "ClientGuid is empty!  Failing."
		
		MessageBox MB_OK "Installation failed due to empty ClientGuid.  Please email support@DesktopBootstrap.com to report this failure."
		Quit
	${EndIf}
	
	WriteRegStr HKCU "Software\DesktopBootstrap" "ClientGuid" "$tentative_or_actual_client_guid"
	WriteRegStr HKLM "Software\DesktopBootstrap" "ClientGuid" "$tentative_or_actual_client_guid"
FunctionEnd


;Call GenerateGuid
;Pop $0 ;contains Guid
Function GenerateGuid
	  ; Guid has 128 bit = 16 byte = 32 hex characters
	  Push $R0
	  Push $R1
	  Push $R2
	  Push $R3
	  Push $R4
	  ;allocate space for character array
	  System::Alloc 16
	  ;get pointer to new space
	  Pop $R1
	  StrCpy $R0 "" ; init
	  ;call the CoCreateGuid api in the ole32.dll
	  System::Call 'ole32::CoCreateGuid(i R1) i .R2'
	  ;if 0 then continue
	  IntCmp $R2 0 continue 
	  ; set error flag
	  SetErrors
	  goto done
	continue:
	  ;byte counter = 0
	  StrCpy $R3 0
	loop:
	    System::Call "*$R1(&v$R3, &i1 .R2)"
	    ;now $R2 is byte at offset $R3
	    ;convert to hex
	    IntFmt $R4 "%X" $R2
	    StrCpy $R4 "00$R4"
	    StrLen $R2 $R4
	    IntOp $R2 $R2 - 2
	    StrCpy $R4 $R4 2 $R2
	    ;append to result
	    StrCpy $R0 "$R0$R4"
	    ;increment byte counter
	    IntOp $R3 $R3 + 1
	    ;if less than 16 then continue
	    IntCmp $R3 16 0 loop
	done:
	  ;cleanup
	  System::Free $R1
	  Pop $R4
	  Pop $R3
	  Pop $R2
	  Pop $R1
	  Exch $R0
FunctionEnd