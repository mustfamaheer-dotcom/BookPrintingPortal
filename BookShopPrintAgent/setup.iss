[Setup]
AppName=DR Bahig Books Portal
AppVersion=1.0.0
AppPublisher=DR Bahig Books
DefaultDirName={commonpf}\BookShopPrintAgent
DefaultGroupName=DR Bahig Books Portal
UninstallDisplayIcon={app}\BookShopPortalUI.exe
OutputDir=.
OutputBaseFilename=DR_Bahig_Books_Portal_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
DisableProgramGroupPage=yes
DisableWelcomePage=no
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=book.ico
WizardImageFile=wizard.bmp
WizardSmallImageFile=wizardSmall.bmp
AppPublisherURL=https://drbaheegbook.runasp.net
AppSupportURL=https://drbaheegbook.runasp.net

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourcePath}\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{commondesktop}\DR Bahig Books Portal"; Filename: "{app}\BookShopPortalUI.exe"; WorkingDir: "{app}"; IconFilename: "{app}\book.ico"
Name: "{group}\DR Bahig Books Portal"; Filename: "{app}\BookShopPortalUI.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall DR Bahig Books Portal"; Filename: "{uninstallexe}"

[Run]
Filename: "schtasks"; Parameters: "/create /tn ""BookShopPrintAgent"" /tr ""'{app}\BookShopPrintAgent.exe'"" /sc onstart /ru SYSTEM /rl highest /f"; Flags: runhidden; StatusMsg: "Creating scheduled task (auto-start on boot)..."
Filename: "{app}\BookShopPortalUI.exe"; Flags: nowait runhidden; Description: "Start DR Bahig Books Portal now"

[UninstallRun]
Filename: "schtasks"; Parameters: "/delete /tn ""BookShopPrintAgent"" /f"; Flags: runhidden; RunOnceId: "RemoveScheduledTask"

[Code]
function CheckNet: Boolean;
var
  v: String;
begin
  Result := RegQueryStringValue(HKLM64,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App\10.0.0',
    'Version', v);
  if not Result then
    Result := RegQueryStringValue(HKLM64,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\10.0.0',
    'Version', v);
end;

function InitializeSetup: Boolean;
begin
  Result := CheckNet;
  if not Result then
  begin
    MsgBox('This app requires .NET Desktop Runtime 10.0 (x64).'#13#10#13#10'Please download from:'#13#10'https://dotnet.microsoft.com/en-us/download/dotnet/10.0', mbInformation, MB_OK);
    Result := False;
  end;
end;
