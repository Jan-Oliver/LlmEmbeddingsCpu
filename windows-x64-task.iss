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
OutputBaseFilename=LlmEmbeddingsCpuInstallerX64

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
Source: ".\src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-x64\publish\LlmEmbeddingsCpu.App.exe"; DestDir: "{app}"; Flags: ignoreversion

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
; Register the autostart Task‑Scheduler job (single line, no breaks)
; --------------------------------------------------------------------
Filename: "{sys}\schtasks.exe"; \
Parameters: "/Create /F /RL HIGHEST /SC ONLOGON /DELAY 0000:10 /TN ""LLMEmbeddingsCpuHooks"" /TR ""'{app}\LlmEmbeddingsCpu.App.exe'"" /IT"; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Registering autostart task..."

; Patch battery + time-limit settings --------------------------------
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command ""$t = Get-ScheduledTask -TaskName 'LLMEmbeddingsCpuHooks'; $s = $t.Settings; $s.DisallowStartIfOnBatteries = $false; $s.StopIfGoingOnBatteries = $false; $s.ExecutionTimeLimit = 'PT0S'; Set-ScheduledTask -TaskName 'LLMEmbeddingsCpuHooks' -Settings $s"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Removing battery & time‑limit restrictions..."

; --- Run the agent once right now -------------------------
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