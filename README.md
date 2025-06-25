# LLM Embeddings CPU Background Agent

## 1\. Project Overview

The `LLM Embeddings CPU Background Agent` is a .NET-based Windows application that runs in the background to monitor user activity (keyboard, mouse, and active windows). It processes this activity to generate text embeddings using a local ONNX model, which can be used for various data analysis and machine learning tasks. The agent is designed to be lightweight and is deployed via a robust Inno Setup installer that handles all necessary prerequisites.

### 1.1. Codebase Structure

The solution is organized into several projects, following a clean, decoupled architecture:

  * **`LlmEmbeddingsCpu.Core`**: The core of the application. It contains the essential domain models (like `ActiveWindowLog.cs`, `Embeddings.cs`) and interfaces (`IEmbeddingService.cs`) that define the contracts for services across the application.
  * **`LlmEmbeddingsCpu.Services`**: Contains the main business logic.
      * `IntfloatEmbeddingService`: Handles the creation of text embeddings using the ONNX model. This is the primary consumer of the native dependencies.
      * `KeyboardMonitorService`, `MouseMonitorService`, `WindowMonitorrService`: These services are responsible for hooking into the Windows OS to capture user input and active window information.
      * `ScheduledProcessingService`: Manages the background timer that periodically processes the collected data.
  * **`LlmEmbeddingsCpu.Data`**: Responsible for all data persistence. It writes the logs for keyboard, mouse, and window activity to local files.
  * **`LlmEmbeddingsCpu.App`**: The main executable project (`Program.cs`). It's a Windows application responsible for initializing and wiring up all the services (Dependency Injection) and starting the background processing.
  * **`LlmEmbeddingsCpu.Common`**: A shared library for common utilities and extension methods used across the other projects.

## 2\. Development Setup and Running Locally

Before you can build the production installers, you'll need to set up your local development environment.

### 2.1. Model Setup

The embedding service requires a pre-trained ONNX model and its associated tokenizer configuration files. While most configuration files are included in the repository due to their small size, the main model file (`model.onnx`) is too large for version control and must be added manually.

1.  **Locate the Directory**: Navigate to the following folder within the project structure:
    `src/LlmEmbeddingsCpu.App/deps/intfloat/multilingual-e5-small/`

2.  **Add the Model File**: Place the `model.onnx` file into this directory. The final contents of the folder should include:

      * `config.json` (already present)
      * `model.onnx` **(You must add this manually)**
      * `special_tokens_map.json` (already present)
      * `tokenizer.json` (already present)
      * `tokenizer_config.json` (already present)

You can download the `intfloat/multilingual-e5-small` `model.onnx` from [AWS here]("https://olli-master-thesis.s3.eu-west-1.amazonaws.com/multilingual-e5-small-onnx.zip") or use the `run_model_conversion.py` script in the `EvaluateLlmEmbeddingsCpu` repository to quickly convert any model to onnx format.

### 2.2. Running the Application for Development

For testing and debugging, you can run the application directly from the command line without creating an installer. Use the `dotnet run` command, pointing it to the main application project.

  * **Run with default architecture:**
    This command builds and runs the project using your machine's default .NET SDK architecture.

    ```bash
    dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj
    ```

  * **Run specifically for x64:**
    This is useful for testing the x64-specific version.

    ```bash
    dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -r win-x64
    ```

  * **Run specifically for ARM64:**
    This is useful for testing the ARM64-specific version.

    ```bash
    dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -r win-arm64
    ```

## 3\. Build and Deployment Workflow

This section outlines the complete process for creating the final production installers.

### 3.1. Step 1: Publishing the Application

The application must first be published as a self-contained, single-file executable for each target architecture. Use the `dotnet publish` command from the root of the repository.

**For x64 Architecture:**

```bash
dotnet publish src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -p:DebugType=None -p:DebugSymbols=false
```

**For ARM64 Architecture:**

```bash
dotnet publish src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -c Release -r win-arm64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -p:DebugType=None -p:DebugSymbols=false
```

These commands will create the executables in the following directories:

  * `.\src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-x64\publish\`
  * `.\src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-arm64\publish\`

### 3.2. Step 2: Creating the Installers with Inno Setup

This project uses **Inno Setup** to create user-friendly installers for the application.

1.  **Prerequisites:**

      * Download and install the **Inno Setup Compiler** from [jrsoftware.org](https://jrsoftware.org/isinfo.php).
      * The required Visual C++ Redistributable executables (`VC_redist.x64.exe` and `VC_redist.arm64.exe`) are already provided in the subfolder named `prerequisites` in your project's root directory.

2.  **Compiling the Installers:**

      * Open the Inno Setup Compiler.
      * Go to `File` \> `Open...` and select the appropriate `.iss` script file (`LlmEmbeddingsCpuInstallerX64.iss` or `LlmEmbeddingsCpuInstallerArm64.iss`).
      * Compile the script by clicking the `Compile` button (or pressing `F9`).
      * By default, the compiled installer (`.exe`) will be placed in an `Output` subfolder in the same directory as the script.

## 4\. Understanding the Inno Setup Installer

The installer does more than just copy files. It intelligently configures the system to ensure the application runs reliably as a background agent.

### 4.1. Core Functionality: The Scheduled Task

The primary mechanism for running the application is a **Windows Scheduled Task**. The installer automatically creates this task, which is configured to launch `LlmEmbeddingsCpu.App.exe` automatically whenever a user logs into the system. This provides a robust and persistent way to run the background agent. 

Note: Using a more complex Windows Service isn't an option as services don't have access to user level keyboard/mouse hooks. Thus, using a service instead of a scheduled task isn't valid for our use case.

The task is created with the following command-line parameters in the `.iss` script's `[Run]` section:

  * `/Create /F`: Creates a new task or forcefully updates it if it already exists.
  * `/RL HIGHEST`: Runs the task with the highest administrative privileges, which is necessary for the low-level hooks to capture keyboard and mouse input from all applications.
  * `/SC ONLOGON`: Sets the trigger to run the task each time any user logs on.
  * `/DELAY 0000:10`: Waits 10 seconds after logon before starting the task. This helps prevent system slowdown during a busy startup period.
  * `/TN "LLMEmbeddingsCpuHooks"`: Assigns the unique, predictable name "LLMEmbeddingsCpuHooks" to the task.
  * `/TR "'{app}\LlmEmbeddingsCpu.App.exe'"`: Specifies the full path to the application executable to run.
  * `/IT`: Allows the task to run even if the user is not actively logged on.

### 4.2. Power Settings Modification

Immediately after creating the task, the installer runs a PowerShell command to modify its settings. This is crucial for a background agent on a laptop. The script ensures that:

  * The task is **not** prevented from starting if the computer is on battery power (`DisallowStartIfOnBatteries = $false`).
  * The task will **not** be stopped if the computer switches to battery power (`StopIfGoingOnBatteries = $false`).
  * The task has no time limit and can run indefinitely (`ExecutionTimeLimit = 'PT0S'`).

### 4.3. Manually Managing the Scheduled Task

If you need to debug or manually control the task, you can use the built-in `schtasks.exe` command-line utility in a Command Prompt or PowerShell window.

  * **View detailed information about the task:**
    ```cmd
    schtasks /Query /TN "LLMEmbeddingsCpuHooks" /V /FO LIST
    ```
  * **Forcefully delete the task:**
    ```cmd
    schtasks /Delete /TN "LLMEmbeddingsCpuHooks" /F
    ```
  * **Manually run the task right now:**
    ```cmd
    schtasks /Run /TN "LLMEmbeddingsCpuHooks"
    ```

### 4.3. Manually Managing the Scheduled Task via Task Scheduler UI

For a graphical interface, you can use the built-in `Windows Task Scheduler`.

  *  Press the Windows Key, type `Task Scheduler`, and open the application.
  * In the left-hand pane, click on the `Task Scheduler Library` folder.
  * In the center pane, you will see a list of all scheduled tasks on the system. Find the task named `LLMEmbeddingsCpuHooks` in this list.
  * Once you select the task, you can view its properties, triggers, and history, as well as manually run, end, or disable it using the actions in the right-hand pane.

## 5\. The Native Dependency Challenge (and Solution)

During development, a critical issue was identified where the application would run on a development machine but fail on a clean Windows installation.

As it's not intuitive how to approach this issue and how to debug it, here a few tips and an explanation.

### 5.1. The Problem: `System.DllNotFoundException`

The application would crash on startup with a `DllNotFoundException`, indicating that `hf_tokenizers.dll` could not be loaded. This was misleading, as the file itself was present.

The root cause was not the DLL itself, but a **missing dependency** of that DLL. The `Tokenizers.DotNet` library, used in `IntfloatEmbeddingService.cs`, is a wrapper around a native library (`hf_tokenizers.dll`) built with the Microsoft Visual C++ (MSVC) toolchain. This toolchain creates a dependency on the **Visual C++ Runtime**, specifically `VCRUNTIME140.dll`.

On a clean Windows machine without development tools installed, this runtime is often missing. The Windows loader would successfully find `hf_tokenizers.dll`, but then fail when it tried to load its dependency, `VCRUNTIME140.dll`, resulting in the crash.

### 5.2. The Solution: Bundling Prerequisites

The solution was to make the installer responsible for ensuring this system-level prerequisite is met. This is achieved within the Inno Setup scripts (`.iss` files) by:

1.  **Bundling:** The `VC_redist.x64.exe` and `VC_redist.arm64.exe` installers are included in the `[Files]` section of the respective Inno Setup scripts.
2.  **Checking:** A custom Pascal Script function in the `[Code]` section checks the Windows Registry to see if the VC++ Redistributable is already installed.
3.  **Executing:** The `[Run]` section of the script calls the `VC_redist` installer **silently** and **only if** the check function determines it's missing.

This ensures a seamless installation experience for the end-user on any machine, as the necessary dependencies are handled automatically.

## 6\. Debugging with Process Monitor

A key tool used to diagnose the missing DLL issue was **Process Monitor** (`procmon.exe`) from the Sysinternals Suite. It can trace file system, registry, and process activity, which is perfect for finding `NAME NOT FOUND` errors.

**Basic Debugging Steps:**

1.  **Download and run Process Monitor** from Microsoft.
2.  **Set up the filter:** To avoid being overwhelmed by system-wide events, set a filter to only show events from your application.
      * Go to `Filter` \> `Filter...` (or press `Ctrl+L`).
      * Create a new filter rule: `Process Name` - `is` - `LlmEmbeddingsCpu.App.exe` - `Include`.
      * Add the rule and apply the filter.
3.  **Reproduce the crash:** Run your application. Process Monitor will now only capture events related to it.
4.  **Analyze the log:** After the application crashes, stop capturing events (press `Ctrl+E`). Look through the log for operations with a `Result` of **`NAME NOT FOUND`**. This will show you exactly which file the application tried to load and failed to find, pinpointing the missing dependency (in our case, `VCRUNTIME140.dll` in various system paths).