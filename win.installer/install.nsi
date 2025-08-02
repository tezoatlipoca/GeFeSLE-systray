; Basic installer script for GeFeSLE-systray
!include "MUI2.nsh"
!include "VersionCompare.nsh"

; Update ALL version references (change these for each version)
!define VERSION "0.1.0"

; General Settings  
Name "GeFeSLE-systray"
OutFile "GeFeSLE-systray-${VERSION}-setup.exe"
InstallDir "$PROGRAMFILES\GeFeSLE-systray"
InstallDirRegKey HKCU "Software\GeFeSLE-systray" ""
RequestExecutionLevel admin

; Version Information
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "GeFeSLE-systray"
VIAddVersionKey "FileDescription" "Systray application for GeFeSLE"
VIAddVersionKey "LegalCopyright" "© tezoatlipoca@gmail.com"
VIAddVersionKey "FileVersion" "${VERSION}"

; Interface Settings
!define MUI_ABORTWARNING
!define MUI_ICON "gefesleff.ico" ; Optional: Replace with your icon path

; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; Languages
!insertmacro MUI_LANGUAGE "English"

; Installer Sections
Section "Install"
  SetOutPath "$INSTDIR"

  ; Add files - copy all files from publish folder
  File "..\bin\Release\net8.0\win-x64\publish\GeFeSLE-systray.exe"
  File "..\bin\Release\net8.0\win-x64\publish\*.dll"

  ; Check for existing version
  ReadRegStr $R0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GeFeSLE-systray" "DisplayVersion"
  
  StrCpy $R1 "${VERSION}" ; This installer's version

  ${If} $R0 != ""
    ${VersionCompare} $R0 $R1 $R2
    ${If} $R2 == "1" ; R0 > R1 (installed version is newer)
      MessageBox MB_ICONSTOP "A newer version ($R0) of GeFeSLE-systray is already installed.$\nCurrent installer: $R1$\nInstaller will now close."
      Quit
    ${ElseIf} $R2 == "0" ; R0 == R1 (same version)
      MessageBox MB_ICONQUESTION|MB_YESNO "GeFeSLE-systray version $R0 is already installed. Do you want to reinstall?" IDNO quit_installer
    ${Else}
      MessageBox MB_ICONQUESTION|MB_YESNO "Upgrading GeFeSLE-systray from version $R0 to ${VERSION}. Continue?" IDNO quit_installer
    ${EndIf}
  ${EndIf}

  Goto continue_install
  
  quit_installer:
    Quit
  
  continue_install:

  ; Create Start Menu shortcuts
  CreateDirectory "$SMPROGRAMS\GeFeSLE-systray"
  CreateShortcut "$SMPROGRAMS\GeFeSLE-systray\GeFeSLE-systray.lnk" "$INSTDIR\GeFeSLE-systray.exe"
  CreateShortcut "$SMPROGRAMS\GeFeSLE-systray\Uninstall.lnk" "$INSTDIR\uninstall.exe"

  ; Create uninstaller
  WriteUninstaller "$INSTDIR\uninstall.exe"

  ; Registry entries for uninstaller
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GeFeSLE-systray" "DisplayName" "GeFeSLE-systray"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GeFeSLE-systray" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GeFeSLE-systray" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GeFeSLE-systray" "Publisher" "tezoatlipoca"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GeFeSLE-systray" "InstallLocation" "$INSTDIR"
  
  ; Add to Windows startup (current user only)
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "GeFeSLE-systray" "$\"$INSTDIR\GeFeSLE-systray.exe$\""
  DetailPrint "Added GeFeSLE-systray to Windows startup"
SectionEnd

; Uninstaller Section
Section "Uninstall"
  ; Remove files and folders
  Delete "$INSTDIR\GeFeSLE-systray.exe"
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\uninstall.exe"
  
  ; Remove shortcuts
  Delete "$SMPROGRAMS\GeFeSLE-systray\GeFeSLE-systray.lnk"
  Delete "$SMPROGRAMS\GeFeSLE-systray\Uninstall.lnk"
  RMDir "$SMPROGRAMS\GeFeSLE-systray"
  
  ; Remove directories
  RMDir "$INSTDIR"
  
  ; Remove registry keys
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GeFeSLE-systray"
  
  ; Remove from Windows startup
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "GeFeSLE-systray"
  DetailPrint "Removed GeFeSLE-systray from Windows startup"
SectionEnd