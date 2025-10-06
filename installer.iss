; Inno Setup Installer for ReMux2 with optional FFmpeg download
; Build with Inno Setup 6+ (iscc.exe or GUI)

[Setup]
AppName=ReMux2
AppVersion=1.0.0
DefaultDirName={autopf}\ReMux2
DefaultGroupName=ReMux2
OutputDir=d:\GitHub\ReMux2\dist-installer
OutputBaseFilename=ReMux2Installer
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
DisableDirPage=no
UninstallDisplayIcon={app}\ReMux2.exe
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional tasks:"; Flags: unchecked
; Name: "installffmpeg"; Description: "Download and install FFmpeg (~100–200 MB)"; GroupDescription: "Optional components:"; Flags: unchecked  ; disabled - FFmpeg bundled

[Files]
Source: "d:\GitHub\ReMux2\bin\Release\net9.0-windows7.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "d:\GitHub\ReMux2\icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "d:\GitHub\ReMux2\ffmpeg.exe"; DestDir: "{app}\\ffmpeg"; Flags: ignoreversion
Source: "d:\GitHub\ReMux2\ffprobe.exe"; DestDir: "{app}\\ffmpeg"; Flags: ignoreversion
Source: "d:\GitHub\ReMux2\ffplay.exe"; DestDir: "{app}\\ffmpeg"; Flags: ignoreversion

[Icons]
Name: "{group}\ReMux2"; Filename: "{app}\ReMux2.exe"; IconFilename: "{app}\icon.ico"
Name: "{commondesktop}\ReMux2"; Filename: "{app}\ReMux2.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
; FFmpeg handling is done in [Code]

[Code]
const
  // Primary: Gyan.dev ffmpeg release essentials ZIP (works with .NET ZipFile extraction)
  FFmpegUrl = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-master-latest-win64-gpl.zip';
  // Alternate: Gyan.dev ffmpeg git essentials (ZIP) as fallback
  FFmpegUrlAlt = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-master-latest-win64-gpl.zip';
  FFmpegFolderName = 'ffmpeg';
function URLDownloadToFile(Caller: cardinal; URL, FileName: string; Reserved: cardinal; StatusCB: cardinal): Integer;
external 'URLDownloadToFileA@urlmon.dll stdcall';

function IsFFmpegPresent(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\ffmpeg\bin\ffmpeg.exe')) or
            FileExists(ExpandConstant('{app}\ffmpeg.exe')) or
            (GetEnv('FFMPEG') <> '');
end;

// PowerShell download fallback with explicit TLS 1.2 and suppressed progress
function DownloadWithPowerShell(const Url, DestZip: string): Boolean;
var
  Params: string;
  ResultCode: Integer;
  LogFile: string;
  LogContent: AnsiString;
begin
  LogFile := ExpandConstant('{tmp}\powershell_download_log.txt');
  Params :=
    '-NoProfile -ExecutionPolicy Bypass -Command ' +
    '"$ErrorActionPreference=''Stop''; ' +
    '$ProgressPreference=''SilentlyContinue''; ' +
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    '$dest = ''' + DestZip + '''; ' +
    'try { $headers = @{ ''User-Agent'' = ''Mozilla/5.0 (Windows NT 10.0; Win64; x64)'' }; ' +
    'Invoke-WebRequest -Uri ''' + Url + ''' -OutFile $dest -UseBasicParsing -Headers $headers -TimeoutSec 120 } ' +
    'catch { try { (New-Object System.Net.WebClient).DownloadFile(''' + Url + ''',$dest) } ' +
    'catch { if (Get-Command Start-BitsTransfer -ErrorAction SilentlyContinue) { Start-BitsTransfer -Source ''' + Url + ''' -Destination $dest -ErrorAction Stop } else { throw $_ } } } ' +
    '; if (!(Test-Path $dest) -or ((Get-Item $dest).Length -lt 1048576)) { throw ''Download failed or too small'' }"';
  Log('Executing PowerShell command for download: ' + Params);
  Result := ShellExec('', 'powershell.exe', Params + ' *>&1 | Out-File -FilePath ''' + LogFile + ''' -Encoding UTF8', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  if not Result then
  begin
    Log('PowerShell download command failed with exit code: ' + IntToStr(ResultCode));
    if FileExists(LogFile) then
    begin
      Log('PowerShell download log:');
      // removed duplicate var declaration; LogContent declared at function top
      if LoadStringFromFile(LogFile, LogContent) then
        Log(LogContent);
    end;
  end;
end;

function DownloadSmallFileWithPowerShell(const Url, DestPath: string): Boolean;
var
  Params: string;
  ResultCode: Integer;
begin
  Params :=
    '-NoProfile -ExecutionPolicy Bypass -Command ' +
    '"$ErrorActionPreference=''Stop''; ' +
    '$ProgressPreference=''SilentlyContinue''; ' +
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    '$dest = ''' + DestPath + '''; ' +
    'Invoke-WebRequest -Uri ''' + Url + ''' -OutFile $dest -UseBasicParsing -TimeoutSec 120"';
  Result := ShellExec('', 'powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function DownloadFFmpegZip(const Url, DestZip: string): Boolean;
begin
  // First try via URLDownloadToFile (WinINet)
  Result := URLDownloadToFile(0, Url, DestZip, 0, 0) = 0;
  // Fallback to PowerShell Invoke-WebRequest with TLS 1.2 in case WinINet/TLS/agent issues occur
  if not Result then
  begin
    Log('URLDownloadToFile failed; attempting PowerShell download fallback.');
    Result := DownloadWithPowerShell(Url, DestZip);
  end;
end;

function ExtractZipWithPowerShell(const ZipPath, DestDir: string): Boolean;
var
  Params: string;
  ResultCode: Integer;
begin
  Params :=
    '-NoProfile -ExecutionPolicy Bypass -Command ' +
    '"$ErrorActionPreference=''Stop''; ' +
    'Add-Type -AssemblyName System.IO.Compression.FileSystem; ' +
    '$zip = ''' + ZipPath + '''; $dest = ''' + DestDir + '''; ' +
    'try { [IO.Compression.ZipFile]::ExtractToDirectory($zip, $dest, $true) } ' +
    'catch { ' +
    '  try { if (Test-Path $dest) { Remove-Item -Path $dest -Recurse -Force -ErrorAction SilentlyContinue }; ' +
    '        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $dest, $true) } ' +
    '  catch { throw $_ } ' +
    '}"';

  Result := ShellExec('', 'powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function CopyWithPowerShell(const SrcDir, DstDir: string): Boolean;
var
  Params: string;
  ResultCode: Integer;
begin
  Params := '-NoProfile -ExecutionPolicy Bypass -Command "Copy-Item -Path ''" + SrcDir + "\*'' -Destination ''" + DstDir + "'' -Recurse -Force"';
  Result := ShellExec('', 'powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function GetFirstSubdir(const RootDir: string): string;
var
  FR: TFindRec;
begin
  Result := '';
  if FindFirst(RootDir + '\*', FR) then
  begin
    try
      repeat
        if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if (FR.Name <> '.') and (FR.Name <> '..') then
          begin
            Result := RootDir + '\' + FR.Name;
            break;
          end;
        end;
      until not FindNext(FR);
    finally
      FindClose(FR);
    end;
  end;
end;

procedure CopyFFmpegSmart(const ExtractDir, TargetDir: string; var CopyOk: Boolean);
var
  SubDir: string;
begin
  CopyOk := False;
  if DirExists(ExtractDir + '\bin') then
  begin
    CopyOk := CopyWithPowerShell(ExtractDir + '\bin', TargetDir + '\bin');
  end
  else
  begin
    SubDir := GetFirstSubdir(ExtractDir);
    if (SubDir <> '') and DirExists(SubDir + '\bin') then
      CopyOk := CopyWithPowerShell(SubDir + '\bin', TargetDir + '\bin')
    else
      CopyOk := CopyWithPowerShell(ExtractDir, TargetDir);
  end;
end;

  if CurStep = ssInstall then
  begin
    // FFmpeg download disabled (bundled in installer)
    if False then
    begin
      TargetDir := ExpandConstant('{app}' + '\' + FFmpegFolderName);
      ExtractDir := ExpandConstant('{tmp}\ffmpeg_extract');
      ZipPath := ExpandConstant('{tmp}\ffmpeg.zip');

      if IsFFmpegPresent() then
      begin
        Log('FFmpeg already present; skipping download.');
        exit;
      end;

      CreateDir(ExtractDir);
      CreateDir(TargetDir);

      Log('Starting FFmpeg download from: ' + FFmpegUrl);
  if not DownloadFFmpegZip(FFmpegUrl, ZipPath) then
  begin
    Log('Initial FFmpeg download attempt failed, retrying once...');
    if not DownloadFFmpegZip(FFmpegUrl, ZipPath) then
    begin
      Log('Primary URL failed twice; attempting alternate URL: ' + FFmpegUrlAlt);
      if not DownloadFFmpegZip(FFmpegUrlAlt, ZipPath) then
      begin
        MsgBox('Failed to download FFmpeg from both primary and alternate URLs.'#13#10 +
               'Please check your internet connection or download it manually from:'#13#10 +
               'https://www.gyan.dev/ffmpeg/builds/ (Essentials zip).', mbError, MB_OK);
        exit;
      end;
    end;
  end;

      if not ExtractZipWithPowerShell(ZipPath, ExtractDir) then
      begin
        MsgBox('Failed to extract FFmpeg archive.', mbError, MB_OK);
        exit;
      end;

      // Copy extracted files intelligently (handles nested ffmpeg-* subfolder)
      CopyFFmpegSmart(ExtractDir, TargetDir, CopyOk);
      if not CopyOk then
      begin
        MsgBox('Failed to copy extracted FFmpeg files.', mbError, MB_OK);
        exit;
      end;

      if FileExists(TargetDir + '\bin\ffmpeg.exe') then
      begin
        Log('FFmpeg installed to: ' + TargetDir + '\bin\ffmpeg.exe');
      end
      else if FileExists(TargetDir + '\ffmpeg.exe') then
      begin
        Log('FFmpeg installed to: ' + TargetDir + '\ffmpeg.exe');
      end
      else
      begin
        MsgBox('FFmpeg download succeeded but ffmpeg.exe was not found after extraction.'#13#10 +
               'You may install FFmpeg manually and point the application to it.', mbError, MB_OK);
      end;
    end;
  end;
end;

function IsDotNetDesktopRuntimeInstalled(): Boolean;
var
  Names: TArrayOfString;
  KeyPath: string;
  I: Integer;
  FR: TFindRec;
  Dir: string;
begin
  Result := False;

  // Registry check (x64)
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  if RegGetSubkeyNames(HKLM, KeyPath, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Names[I] <> '' then
      begin
        Result := True;
        exit;
      end;
    end;
  end;

  // Registry check (x86)
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedfx\Microsoft.WindowsDesktop.App';
  if not Result and RegGetSubkeyNames(HKLM, KeyPath, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Names[I] <> '' then
      begin
        Result := True;
        exit;
      end;
    end;
  end;

  // File system fallback (x64 Program Files)
  if not Result then
  begin
    Dir := ExpandConstant('{pf}\\dotnet\\shared\\Microsoft.WindowsDesktop.App');
    if DirExists(Dir) then
    begin
      if FindFirst(Dir + '\\*', FR) then
      begin
        try
          repeat
            if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
            begin
              Result := True;
              break;
            end;
          until not FindNext(FR);
        finally
          FindClose(FR);
        end;
      end;
    end;
  end;

  // File system fallback (x86 Program Files)
  if not Result then
  begin
    Dir := ExpandConstant('{pf32}\\dotnet\\shared\\Microsoft.WindowsDesktop.App');
    if DirExists(Dir) then
    begin
      if FindFirst(Dir + '\\*', FR) then
      begin
        try
          repeat
            if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
            begin
              Result := True;
              break;
            end;
          until not FindNext(FR);
        finally
          FindClose(FR);
        end;
      end;
    end;
  end;
end;

procedure InitializeWizard;
begin
  // Removed .NET runtime prompt per user request; application will handle runtime detection at startup.
end;