; ====================================================================
;  LlmEmbeddingsCpu – scheduled‑task version (no Windows-service)
;  Tested with Inno Setup 6.3+
; ====================================================================

[Setup]
; --- Basic information ------------------------------------------------
AppName=LLM Embeddings CPU Background Agent
AppVersion=1.0.0
AppPublisher=Your Name or Company

; --- Where to install --------------------------------------------------
DefaultDirName={autopf}\LLM Embeddings CPU
; (Start‑menu folder—optional)
DefaultGroupName=LLM Embeddings CPU         

; --- Output EXE --------------------------------------------------------
OutputBaseFilename=LlmEmbeddingsCpuInstaller

; --- Compression / looks ----------------------------------------------
;Compression=lzma
;SolidCompression=yes
Compression=none
SolidCompression=no
WizardStyle=modern

; --- We need admin rights to write to Program Files and create a task --
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; ----------------------------------------------------------------------
; 1 – Copy files
; ----------------------------------------------------------------------
[Files]
; --- SINGLE‑FILE BUILD -------------------------------------------------
; Publish with:
;   dotnet publish -c Release -r win-x64 ^
;       -p:PublishSingleFile=true -p:SelfContained=true
;
; Then point Source to that one EXE:
Source: ".\src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-arm64\publish\LlmEmbeddingsCpu.App.exe"; \
    DestDir: "{app}"; Flags: ignoreversion

; ----------------------------------------------------------------------
; 2 – Create folders
; ----------------------------------------------------------------------
[Dirs]
; Logs folder – grant Users “Modify” so the background agent can write.
Name: "{app}\logs"; Permissions: users-modify


[Run]
; --------------------------------------------------------------------
; Register the autostart Task‑Scheduler job (single line, no breaks)
; --------------------------------------------------------------------
Filename: "{sys}\schtasks.exe"; \
Parameters: "/Create /F /RL HIGHEST /SC ONLOGON /DELAY 0000:10 /TN ""LLMEmbeddingsCpuHooks"" /TR ""{app}\LlmEmbeddingsCpu.App.exe"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Registering autostart task..."

; Patch battery + time‑limit settings --------------------------------
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command ""$t = Get-ScheduledTask -TaskName 'LLMEmbeddingsCpuHooks'; $s = $t.Settings; $s.DisallowStartIfOnBatteries = $false; $s.StopIfGoingOnBatteries = $false; $s.ExecutionTimeLimit = 'PT0S'; Set-ScheduledTask -TaskName 'LLMEmbeddingsCpuHooks' -Settings $s"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Removing battery & time‑limit restrictions..."

; --- Run the agent once right now -------------------------
Filename: "{app}\LlmEmbeddingsCpu.App.exe"; \
Flags: runhidden nowait; \
StatusMsg: "Launching background agent..."

; ----------------------------------------------------------------------
; 4 – Uninstall: clean up
; ----------------------------------------------------------------------
[UninstallRun]
; Remove the scheduled task
Filename: "schtasks.exe"; \
Parameters: "/Delete /TN ""LLMEmbeddingsCpuHooks"" /F"; \
Flags: runhidden waituntilterminated

; ----------------------------------------------------------------------
; 5 – Delete logs on uninstall -------------------------------
; ----------------------------------------------------------------------
[UninstallDelete]
Type: files;   Name: "{app}\logs\*.log"
Type: dirifempty; Name: "{app}\logs"
