[Setup]
; --- Basic Information ---
AppName=LLM Embeddings CPU Background Service
AppVersion=1.0.0
AppPublisher=Your Name or Company

; --- Installation Paths ---
; {autopf} resolves to C:\Program Files or C:\Program Files (x86)
; The installer will create a subfolder named "LLM Embeddings CPU" within Program Files
DefaultDirName={autopf}\LLM Embeddings CPU
; DefaultGroupName is used for the Start Menu folder (less relevant for a service)
DefaultGroupName=LLM Embeddings CPU

; --- Installer Output ---
; This is the name of the .exe file that Inno Setup will create
OutputBaseFilename=LlmEmbeddingsCpuInstaller

; --- Compression Settings ---
Compression=lzma
SolidCompression=yes

; --- Installer Appearance ---
WizardStyle=modern

; --- Privileges Required ---
; REQUIRED to install a Windows Service and write to Program Files
PrivilegesRequired=admin

; --- License (Optional) ---
; LicenseFile=path\to\your\license.txt ; Uncomment and update if you have a license file


[Files]
; --- Application Files ---
; This section copies your application's published files to the installation directory ({app})
; Source: MUST be the path on YOUR computer to the published output folder from dotnet publish
; Use "*.*" or "*" to include all files and subdirectories.
; Example: Source: "C:\YourProject\bin\Release\net9.0-windows\win-x64\publish\*";
; Example for ARM64: Source: "C:\YourProject\bin\Release\net9.0-windows\win-arm64\publish\*";
Source: "C:\Mac\Home\Documents\Projects\master-thesis\LlmEmbeddingsCpu\src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-arm64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; --- NSSM Executable ---
; This section copies the NSSM executable to the installation directory ({app})
; Source: MUST be the path on YOUR computer to the downloaded nssm.exe (use the win64 version)
; Example: Source: "C:\Downloads\nssm\win64\nssm.exe";
Source: "C:\Mac\Home\Documents\Projects\master-thesis\LlmEmbeddingsCpu\src\LlmEmbeddingsCpu.App\deps\nssm\nssm.exe"; DestDir: "{app}"; Flags: ignoreversion


[Dirs]
; --- Create Logs Directory ---
; Creates the 'logs' subdirectory within the application installation folder ({app})
; Permissions: Grant 'Modify' permission to 'Users' so the service (often running as SYSTEM or LocalService)
; can write log files here.
Name: "{app}\logs"; Permissions: users-modify


[Run]
; --- Commands to Install and Configure the Service using NSSM ---
; These commands are executed AFTER files are copied.
; Filename: "{app}\nssm.exe" refers to the NSSM executable in the installation directory.
; Parameters: These are the command-line arguments for nssm.exe.
; Flags: runhidden prevents a console window, waituntilterminated pauses the installer until the command finishes.

; 1. Install the service
;    Parameters: "install [Service Name] [PathToYourApp.exe]"
Filename: "{app}\nssm.exe"; Parameters: "install ""LlmEmbeddingsCpuService"" ""{app}\LlmEmbeddingsCpu.App.exe"""; WorkingDir: "{app}"; StatusMsg: "Installing background service..."; Flags: runhidden waituntilterminated

; 2. Set the application working directory (Crucial for finding deps, logs, etc.)
;    Parameters: "set [Service Name] AppDirectory [WorkingDirectory]"
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" AppDirectory ""{app}"""; Flags: runhidden waituntilterminated

; 3. Set Standard Output (stdout) log file path
;    Parameters: "set [Service Name] AppStdout [PathToStdoutLogFile]"
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" AppStdout ""{app}\logs\stdout.log"""; Flags: runhidden waituntilterminated

; 4. Set Standard Error (stderr) log file path
;    Parameters: "set [Service Name] AppStderr [PathToStderrLogFile]"
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" AppStderr ""{app}\logs\stderr.log"""; Flags: runhidden waituntilterminated

; 5. Set Service Display Name (The friendly name in services.msc)
;    Parameters: "set [Service Name] DisplayName ""Your Display Name""
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" DisplayName ""LLM CPU Background Service"""; Flags: runhidden waituntilterminated

; 6. Set Service Description
;    Parameters: "set [Service Name] Description ""Your Service Description""
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" Description ""Monitors system input for LLM processing and analysis in the background."""; Flags: runhidden waituntilterminated

; 7. Configure Automatic Startup
;    Parameters: "set [Service Name] Start SERVICE_AUTO_START"
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" Start SERVICE_AUTO_START"; Flags: runhidden waituntilterminated

; 8. Configure Restart on Failure (Restart after 1 second delay)
;    Parameters: "set [Service Name] Restart ACTION_RESTART DELAY [milliseconds]"
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" Restart ACTION_RESTART DELAY 1000"; Flags: runhidden waituntilterminated

; 9. Configure Restart Throttle (Minimum time between restarts to prevent infinite loops)
;    Parameters: "set [Service Name] RestartThrottle [milliseconds]"
Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" RestartThrottle 5000"; Flags: runhidden waituntilterminated

; 10. Configure Restart Window (Time window for counting failures before stopping automatic restarts)
;     Parameters: "set [Service Name] RestartWindow [seconds]"
;     (Optional, defaults often sufficient)
; Filename: "{app}\nssm.exe"; Parameters: "set ""LlmEmbeddingsCpuService"" RestartWindow 300"; Flags: runhidden waituntilterminated

; 11. Start the service after installation
;     Parameters: "start [Service Name]"
;     Flags: nowait - Allows the installer to finish immediately, even if the service is still starting.
Filename: "{app}\nssm.exe"; Parameters: "start ""LlmEmbeddingsCpuService"""; StatusMsg: "Starting background service..."; Flags: runhidden nowait


[UninstallRun]
; --- Commands to Uninstall the Service using NSSM ---
; These commands are executed during uninstallation.
; Filename: "{app}\nssm.exe" refers to NSSM in the directory where the app was installed.
; Flags: waituntilterminated pauses the uninstaller, suppresserrors prevents errors if service is already stopped.

; 1. Stop the service
Filename: "{app}\nssm.exe"; Parameters: "stop ""LlmEmbeddingsCpuService"""; Flags: runhidden waituntilterminated

; 2. Remove the service definition
;    'confirm' bypasses the "Are you sure?" prompt
Filename: "{app}\nssm.exe"; Parameters: "remove ""LlmEmbeddingsCpuService"" confirm"; Flags: runhidden waituntilterminated


; --- Optional: Delete Logs on Uninstall ---
; Add a [UninstallDelete] section to remove files/directories created during install.
; Be careful with this, especially if users want to keep logs after uninstall.
; [UninstallDelete]
; Name: "{app}\logs"; Type: folderexclempty ; Delete logs folder if empty (or change Type to folder to delete always)
; Name: "{app}\logs\*.log"; Type: files ; Delete log files


; --- Icons (Optional) ---
; If you have an icon file (.ico) for your application, uncomment and update this section
; You can then use it for installer icon, program icon etc.
; [Icons]
; Name: "{group}\Uninstall"; Filename: "{uninstallexe}"
; Name: "{group}\{cm:ProgramOnTheWeb,LLM Embeddings CPU}"; Filename: "https://yourwebsite.com" ; Example web link
; Name: "{group}\Configuration Tool"; Filename: "{app}\YourConfigTool.exe" ; If you have a separate config app


; --- Languages (Optional) ---
; Uncomment and list languages you want the installer to support
; [Languages]
; Name: en; MessagesFile: compiler:Default.isl
; Name: de; MessagesFile: compiler:Languages\German.isl