; StatusLightCheckerSetup.iss
; Inno Setup 6 installer script for Status Light Checker
;
; Build via MSBuild (StatusLightChecker.Installer.csproj) which passes all
; /D defines automatically. To compile manually:
;   ISCC.exe /DAppVersion=2.0.0 /DClientPublishDir=<path> /DServicePublishDir=<path> /DOutputDir=<path> StatusLightCheckerSetup.iss

; ---------------------------------------------------------------------------
; External defines — all supplied by the csproj at build time via /D flags.
; If any are missing, the #error directive stops the build with a clear message.
; ---------------------------------------------------------------------------

#ifndef AppVersion
  #error AppVersion not defined. Pass /DAppVersion=x.y.z to ISCC.exe.
#endif

#ifndef ClientPublishDir
  #error ClientPublishDir not defined. Pass /DClientPublishDir=<path> to ISCC.exe.
#endif

#ifndef ServicePublishDir
  #error ServicePublishDir not defined. Pass /DServicePublishDir=<path> to ISCC.exe.
#endif

#ifndef OutputDir
  #define OutputDir "..\..\build\bin\StatusLightChecker.Installer\Release"
#endif

; ---------------------------------------------------------------------------
; [Setup]
; ---------------------------------------------------------------------------

[Setup]
AppName=Status Light Checker
AppVersion={#AppVersion}
AppVerName=Status Light Checker {#AppVersion}
AppPublisher=Jublin.xyz
AppPublisherURL=https://github.com/jublin
AppSupportURL=https://github.com/jublin/StatusLightCheckerWPF/issues
AppUpdatesURL=https://github.com/jublin/StatusLightCheckerWPF/releases

AppId={{E220399D-9D5E-4163-AB48-CABB103A67A1}}
; AppId is the stable product GUID. Must never change between versions.

DefaultDirName={autopf}\JublinXYZ\StatusLightChecker
; {autopf} resolves to Program Files in admin mode and
; {localappdata}\Programs in non-admin mode (Inno Setup 6.1+).

DefaultGroupName=Status Light Checker
DisableProgramGroupPage=yes

OutputDir={#OutputDir}
OutputBaseFilename=StatusLightCheckerSetup

SetupIconFile=..\StatusLightChecker\icon.ico
UninstallDisplayIcon={app}\Client\StatusLightChecker.exe

Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Allow the user to choose per-user or per-machine at install time.
; Non-admin (per-user) installs will NOT register the Windows Service — see [Code].
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

MinVersion=10.0

; ---------------------------------------------------------------------------
; [Languages]
; ---------------------------------------------------------------------------

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ---------------------------------------------------------------------------
; [Dirs]
; ---------------------------------------------------------------------------

[Dirs]
; Writable data directory for logs and SQLite DB.
; Per-machine → %ProgramData%\Jublin\StatusLightChecker
; Per-user    → %AppData%\Jublin\StatusLightChecker
; uninsneveruninstall: leave behind on uninstall so user data isn't wiped.
Name: "{code:GetDataDir}\Service"; Flags: uninsneveruninstall
Name: "{code:GetDataDir}\Service\logs"; Flags: uninsneveruninstall
Name: "{code:GetDataDir}\Client"; Flags: uninsneveruninstall
Name: "{code:GetDataDir}\Client\logs"; Flags: uninsneveruninstall

; ---------------------------------------------------------------------------
; [Files]
; ---------------------------------------------------------------------------

[Files]
; Client binaries — all self-contained publish output
Source: "{#ClientPublishDir}\*"; \
  DestDir: "{app}\Client"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; Service binaries — all self-contained publish output
Source: "{#ServicePublishDir}\*"; \
  DestDir: "{app}\Service"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; ---------------------------------------------------------------------------
; [Icons]
; ---------------------------------------------------------------------------

[Icons]
Name: "{group}\Status Light Checker"; \
  Filename: "{app}\Client\StatusLightChecker.exe"; \
  WorkingDir: "{app}\Client"
Name: "{group}\Uninstall Status Light Checker"; \
  Filename: "{uninstallexe}"

; ---------------------------------------------------------------------------
; [Run] — post-install actions
; ---------------------------------------------------------------------------

[Run]
; Register the Windows Service. sc.exe requires a space after binPath=.
; Only runs in admin (all-users) install mode.
Filename: "{sys}\sc.exe"; \
  Parameters: "create ""StatusLightCheckerService"" binPath= ""{app}\Service\StatusLightChecker.Service.exe"" type= own start= auto error= normal displayname= ""Status Light Checker Service"""; \
  Flags: runhidden waituntilterminated; \
  StatusMsg: "Registering Status Light Checker Service..."; \
  Check: IsAdminInstallMode

Filename: "{sys}\sc.exe"; \
  Parameters: "description ""StatusLightCheckerService"" ""Monitors application status for LED light control"""; \
  Flags: runhidden waituntilterminated; \
  Check: IsAdminInstallMode

; Start= wait but don't block — service startup may legitimately take a moment.
Filename: "{sys}\sc.exe"; \
  Parameters: "start ""StatusLightCheckerService"""; \
  Flags: runhidden waituntilterminated; \
  StatusMsg: "Starting Status Light Checker Service..."; \
  Check: IsAdminInstallMode

; Optionally launch the client after install (user can uncheck)
Filename: "{app}\Client\StatusLightChecker.exe"; \
  Description: "Launch Status Light Checker"; \
  Flags: nowait postinstall skipifsilent

; ---------------------------------------------------------------------------
; [UninstallRun] — pre/post-uninstall actions
; ---------------------------------------------------------------------------

[UninstallRun]
; Stop and delete the service before files are removed.
Filename: "{sys}\sc.exe"; \
  Parameters: "stop ""StatusLightCheckerService"""; \
  Flags: runhidden waituntilterminated; \
  Check: IsAdminInstallMode

Filename: "{sys}\sc.exe"; \
  Parameters: "delete ""StatusLightCheckerService"""; \
  Flags: runhidden waituntilterminated; \
  Check: IsAdminInstallMode

; ---------------------------------------------------------------------------
; [Code] — Pascal script
; ---------------------------------------------------------------------------

[Code]

{ Returns the writable data directory appropriate for the install mode. }
function GetDataDir(Param: String): String;
begin
  if IsAdminInstallMode then
    Result := ExpandConstant('{commonappdata}\Jublin\StatusLightChecker')
  else
    Result := ExpandConstant('{userappdata}\Jublin\StatusLightChecker');
end;

{ Check registry for any installed .NET Desktop Runtime 10.x (64-bit). }
function IsDotNetDesktopRuntimeInstalled: Boolean;
const
  RegKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM64, RegKey, Names) then
    for I := 0 to High(Names) do
      if Copy(Names[I], 1, 3) = '10.' then
      begin
        Result := True;
        Break;
      end;
end;

{ Download .NET Desktop Runtime 10 via PowerShell and install silently. }
function DownloadAndInstallDotNet: Boolean;
var
  InstallerPath, PSArgs: String;
  RC: Integer;
begin
  Result := False;
  InstallerPath := ExpandConstant('{tmp}\windowsdesktop-runtime-10-x64.exe');

  PSArgs := '-NoProfile -NonInteractive -Command ' +
    '"Invoke-WebRequest -Uri ''https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe''' +
    ' -OutFile ''' + InstallerPath + '''"';

  if not Exec('powershell.exe', PSArgs, '', SW_HIDE, ewWaitUntilTerminated, RC) or (RC <> 0) then
  begin
    MsgBox(
      'Failed to download .NET Desktop Runtime 10.' + #13#10 +
      'Please install it manually from https://dotnet.microsoft.com/download/dotnet/10.0' + #13#10 +
      'then re-run this installer.',
      mbError, MB_OK);
    Exit;
  end;

  Exec(InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, RC);
  { RC=0: success; RC=3010: success, reboot required — both are fine. }
  Result := (RC = 0) or (RC = 3010);
  if not Result then
    MsgBox(
      '.NET Desktop Runtime 10 installer exited with code ' + IntToStr(RC) + '.' + #13#10 +
      'Please install it manually and re-run this installer.',
      mbError, MB_OK);
end;

{ Abort setup if .NET Desktop Runtime 10 is absent and user declines to install it. }
function InitializeSetup: Boolean;
begin
  Result := True;
  if IsDotNetDesktopRuntimeInstalled then
    Exit;

  if MsgBox(
    '.NET Desktop Runtime 10.0 is required but was not found on this machine.' + #13#10 + #13#10 +
    'Click OK to download and install it now (~55 MB),' + #13#10 +
    'or Cancel to exit the installer.',
    mbConfirmation, MB_OKCANCEL) <> IDOK then
  begin
    Result := False;
    Exit;
  end;

  Result := DownloadAndInstallDotNet;
end;

{ Warn the user if they chose non-admin install: service won't run. }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssInstall) and not IsAdminInstallMode then
  begin
    MsgBox(
      'You are installing Status Light Checker for the current user only.' + #13#10 + #13#10 +
      'The background Windows Service will NOT be installed.' + #13#10 +
      'The application requires the service to monitor Teams status.' + #13#10 + #13#10 +
      'To install with full functionality, cancel and re-run the installer' + #13#10 +
      'by right-clicking and selecting "Run as administrator".',
      mbInformation,
      MB_OK
    );
  end;
end;
