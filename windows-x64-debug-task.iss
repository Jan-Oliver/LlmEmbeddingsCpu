; ====================================================================
;  LlmEmbeddingsCpu – scheduled‑task version (no Windows-service)
;  Tested with Inno Setup 6.3+
; ====================================================================

[Setup]
; --- Basic information ------------------------------------------------
AppName=LLM Embeddings CPU Background Agent
AppVersion=1.0.0
AppPublisher=ETHZ

; --- Where to install --------------------------------------------------
DefaultDirName={autopf}\LLM Embeddings CPU
; (Start‑menu folder—optional)
DefaultGroupName=LLM Embeddings CPU

; --- Output EXE --------------------------------------------------------
OutputBaseFilename=LlmEmbeddingsCpuDebugVersionInstallerX64

; --- Compression / looks ----------------------------------------------
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; --- We need admin rights to write to Program Files and create a task --
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; ----------------------------------------------------------------------
; Copy files
; ----------------------------------------------------------------------
[Files]
; --- SINGLE‑FILE BUILD -------------------------------------------------
; Publish with:
;    dotnet publish -c Release -r win-x64 ^
;       -p:PublishSingleFile=true -p:SelfContained=true
;
; Then point Source to that one EXE:
Source: ".\src\LlmEmbeddingsCpu.App\bin\Debug\net9.0-windows\win-x64\publish\LlmEmbeddingsCpu.App.exe"; DestDir: "{app}"; Flags: ignoreversion

; // Include the VC++ Redistributable installer.
; // It will be extracted to a temp folder and deleted after use.
Source: "prerequisites\VC_redist.x64.exe"; DestDir: {tmp}; Flags: deleteafterinstall



; // Check if the prerequisite is already installed.
; // This prevents the installer from running on every installation.
[Code]
function ShouldInstallVcRedist: Boolean;
var
  Installed: Cardinal;
begin
  // The VC++ 2015-2022 x64 redistributable registers itself here.
  // We check if the 'Installed' value is present and set to 1.
  if not RegQueryDwordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then
  begin
    // Key or value doesn't exist, so it's definitely not installed.
    Result := True;
    Exit;
  end;

  // If the 'Installed' value is 1, it's installed. Otherwise, it might be a partial/broken install, so we should run it.
  Result := (Installed <> 1);
end;


[Run]
; --------------------------------------------------------------------
; // First, run the VC++ Redistributable installer if needed.
; // This MUST be the first item in [Run] to ensure dependencies are met.
; --------------------------------------------------------------------
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft VC++ Runtime..."; Check: ShouldInstallVcRedist

; --------------------------------------------------------------------
; Task 1: Logger - runs on user logon continuously
; --------------------------------------------------------------------
Filename: "{sys}\schtasks.exe"; \
Parameters: "/Create /F /RL HIGHEST /SC ONLOGON /DELAY 0000:10 /TN ""LLMEmbeddingsCpuLogger"" /TR ""'{app}\LlmEmbeddingsCpu.App.exe' --logger"" /IT"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Registering logger task..."

; Patch battery + time-limit settings for Logger
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command ""$t = Get-ScheduledTask -TaskName 'LLMEmbeddingsCpuLogger'; $s = $t.Settings; $s.DisallowStartIfOnBatteries = $false; $s.StopIfGoingOnBatteries = $false; $s.ExecutionTimeLimit = 'PT0S'; Set-ScheduledTask -TaskName 'LLMEmbeddingsCpuLogger' -Settings $s"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Configuring logger task settings..."

; --------------------------------------------------------------------
; Task 2: Nightly Cron - runs daily at midnight
; --------------------------------------------------------------------
Filename: "{sys}\schtasks.exe"; \
Parameters: "/Create /F /RL HIGHEST /SC DAILY /ST 00:00 /TN ""LLMEmbeddingsCpuCron"" /TR ""'{app}\LlmEmbeddingsCpu.App.exe' --cron-processor"" /IT"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Registering nightly cron task..."

; Configure nightly cron task settings (wake computer and run on battery)
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command ""$t = Get-ScheduledTask -TaskName 'LLMEmbeddingsCpuCron'; $s = $t.Settings; $s.DisallowStartIfOnBatteries = $false; $s.StopIfGoingOnBatteries = $false; $s.WakeToRun = $true; $s.ExecutionTimeLimit = 'PT0S'; Set-ScheduledTask -TaskName 'LLMEmbeddingsCpuCron' -Settings $s"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Configuring nightly cron task settings..."

; --------------------------------------------------------------------
; Task 3: Aggregator - runs hourly
; --------------------------------------------------------------------
Filename: "{sys}\schtasks.exe"; \
Parameters: "/Create /F /RL HIGHEST /SC HOURLY /TN ""LLMEmbeddingsCpuAggregator"" /TR ""'{app}\LlmEmbeddingsCpu.App.exe' --aggregator"" /IT"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Registering aggregator task..."

; Configure aggregator task settings
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command ""$t = Get-ScheduledTask -TaskName 'LLMEmbeddingsCpuAggregator'; $s = $t.Settings; $s.DisallowStartIfOnBatteries = $false; $s.StopIfGoingOnBatteries = $false; $s.ExecutionTimeLimit = 'PT0S'; Set-ScheduledTask -TaskName 'LLMEmbeddingsCpuAggregator' -Settings $s"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Configuring aggregator task settings..."

; --- Run the logger agent once right now -------------------------
Filename: "{app}\LlmEmbeddingsCpu.App.exe"; \
Parameters: "--logger"; \
Flags: runhidden nowait; \
StatusMsg: "Launching background logger agent..."

; ----------------------------------------------------------------------
; Uninstall: clean up
; - Stop any running processes
; - End any running scheduled tasks
; - Remove the scheduled tasks from scheduler
; ----------------------------------------------------------------------
[UninstallRun]
; Stop any running scheduled tasks first
Filename: "schtasks.exe"; \
Parameters: "/End /TN ""LLMEmbeddingsCpuLogger"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Stopping logger task..."

Filename: "schtasks.exe"; \
Parameters: "/End /TN ""LLMEmbeddingsCpuCron"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Stopping cron task..."

Filename: "schtasks.exe"; \
Parameters: "/End /TN ""LLMEmbeddingsCpuAggregator"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Stopping aggregator task..."

; Kill any running instances of the main process
Filename: "taskkill.exe"; \
Parameters: "/F /IM ""LlmEmbeddingsCpu.App.exe"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Stopping application processes..."

; Remove all scheduled tasks
Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuLogger"" /F"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Removing logger task..."

Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuCron"" /F"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Removing cron task..."

Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuAggregator"" /F"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Removing aggregator task..."