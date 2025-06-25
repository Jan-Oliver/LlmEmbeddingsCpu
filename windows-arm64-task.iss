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


; ----------------------------------------------------------------------
; Run tasks after copying files
; ----------------------------------------------------------------------
[Run]
; --- First, run the ARM64 VC++ Redistributable installer if needed. ---
Filename: "{tmp}\VC_redist.arm64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft VC++ Runtime (ARM64)..."; Check: ShouldInstallVcRedist

; --- Register the autostart Task‑Scheduler job ---
Filename: "{sys}\schtasks.exe"; \
Parameters: "/Create /F /RL HIGHEST /SC ONLOGON /DELAY 0000:10 /TN ""LLMEmbeddingsCpuHooks"" /TR ""'{app}\LlmEmbeddingsCpu.App.exe'"" /IT"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Registering autostart task..."

; --- Patch battery + time-limit settings ---
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command ""$t = Get-ScheduledTask -TaskName 'LLMEmbeddingsCpuHooks'; $s = $t.Settings; $s.DisallowStartIfOnBatteries = $false; $s.StopIfGoingOnBatteries = $false; $s.ExecutionTimeLimit = 'PT0S'; Set-ScheduledTask -TaskName 'LLMEmbeddingsCpuHooks' -Settings $s"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Removing battery & time‑limit restrictions..."

; --- Run the agent once right now ---
Filename: "{app}\LlmEmbeddingsCpu.App.exe"; \
Flags: runhidden nowait; \
StatusMsg: "Launching background agent..."


; ----------------------------------------------------------------------
; Uninstall: clean up
; - Remove the task from scheduler
; ----------------------------------------------------------------------
[UninstallRun]
; Remove the scheduled task
Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuHooks"" /F"; \
Flags: runhidden waituntilterminated