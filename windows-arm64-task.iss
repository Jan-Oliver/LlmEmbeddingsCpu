; ====================================================================
;  LlmEmbeddingsCpu – scheduled‑task version (no Windows-service)
;  Tested with Inno Setup 6.3+
; ====================================================================

[Setup]
; --- Basic information ------------------------------------------------
AppName=LLM Embeddings CPU Background Agent
AppVersion=1.0.0
AppPublisher=ETHZ

; --- Architecture-specific settings -----------------------------------
ArchitecturesInstallIn64BitMode=arm64

; --- Where to install --------------------------------------------------
DefaultDirName={autopf}\LLM Embeddings CPU
; (Start‑menu folder—optional)
DefaultGroupName=LLM Embeddings CPU

; --- Output EXE --------------------------------------------------------
OutputBaseFilename=LlmEmbeddingsCpuInstallerArm64

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
; --- SINGLE‑FILE ARM64 BUILD ---
Source: ".\src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-arm64\publish\LlmEmbeddingsCpu.App.exe"; DestDir: "{app}"; Flags: ignoreversion

; --- Include the ARM64 VC++ Redistributable from its subfolder ---
Source: "prerequisites\VC_redist.arm64.exe"; DestDir: {tmp}; Flags: deleteafterinstall


; ----------------------------------------------------------------------
; Check for prerequisites before running tasks
; ----------------------------------------------------------------------
[Code]
function ShouldInstallVcRedist: Boolean;
var
  Installed: Cardinal;
begin
  // The VC++ 2015-2022 ARM64 redistributable registers itself here.
  // Note the 'Arm64' in the registry path.
  if not RegQueryDwordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\Arm64', 'Installed', Installed) then
  begin
    // Key or value doesn't exist, so it's definitely not installed.
    Result := True;
    Exit;
  end;

  // If the 'Installed' value is 1, it's installed. Otherwise, we should run it.
  Result := (Installed <> 1);
end;


[Run]
; --- First, run the ARM64 VC++ Redistributable installer if needed. ---
Filename: "{tmp}\VC_redist.arm64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft VC++ Runtime (ARM64)..."; Check: ShouldInstallVcRedist

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

; --- Run the logger agent once right now ---
Filename: "{app}\LlmEmbeddingsCpu.App.exe"; \
Parameters: "--logger"; \
Flags: runhidden nowait; \
StatusMsg: "Launching background logger agent..."


; ----------------------------------------------------------------------
; Uninstall: clean up
; - Remove the task from scheduler
; ----------------------------------------------------------------------
[UninstallRun]
; Remove all scheduled tasks
Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuLogger"" /F"; \
Flags: runhidden waituntilterminated

Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuCron"" /F"; \
Flags: runhidden waituntilterminated

Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuAggregator"" /F"; \
Flags: runhidden waituntilterminated