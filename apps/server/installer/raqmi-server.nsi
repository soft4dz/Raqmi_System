; Raqmi System Server — installateur NSIS
!include "MUI2.nsh"

!define PRODUCT_NAME "Raqmi System Server"
!define PRODUCT_VERSION "0.1.0"
!define PRODUCT_PUBLISHER "Raqmi System"
!define INSTALL_DIR "$PROGRAMFILES64\Raqmi System Server"

Name "${PRODUCT_NAME}"
OutFile "..\installers\RaqmiSystemServer-Setup.exe"
InstallDir "${INSTALL_DIR}"
RequestExecutionLevel admin
Unicode true

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "French"

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "..\dist\server-bundle\*.*"

  SetOutPath "$INSTDIR\scripts"
  File "scripts\*.ps1"

  SetOutPath "$INSTDIR\admin"
  File "admin\index.html"

  SetOutPath "$INSTDIR\postgresql"
  File /nonfatal /r "assets\postgresql\*.*"

  CreateDirectory "$COMMONAPPDATA\Raqmi System"
  CreateDirectory "$COMMONAPPDATA\Raqmi System\storage"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortCut "$DESKTOP\Raqmi Server Admin.lnk" "$INSTDIR\admin\index.html"
  CreateShortCut "$SMPROGRAMS\Raqmi System Server\Raqmi Server Admin.lnk" "$INSTDIR\admin\index.html"
  CreateShortCut "$SMPROGRAMS\Raqmi System Server\Désinstaller.lnk" "$INSTDIR\Uninstall.exe"

  ; Post-install : init PostgreSQL + services
  ExecWait 'powershell.exe -ExecutionPolicy Bypass -File "$INSTDIR\scripts\init-postgres.ps1" -InstallRoot "$INSTDIR"'
  ExecWait 'powershell.exe -ExecutionPolicy Bypass -File "$INSTDIR\scripts\install-services.ps1" -InstallRoot "$INSTDIR"'
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\Raqmi Server Admin.lnk"
  RMDir /r "$SMPROGRAMS\Raqmi System Server"
  RMDir /r "$INSTDIR"
SectionEnd
