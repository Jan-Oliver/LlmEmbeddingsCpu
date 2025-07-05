# LLM Embeddings CPU - Comprehensive Technical Documentation

This document provides a complete technical understanding of the LLM Embeddings CPU codebase, designed for Computer Science, Electrical Engineering, and Data Science master's degree students. It covers the architecture, implementation details, deployment process, and debugging strategies.

## Table of Contents

1. [Project Architecture and Structure](#1-project-architecture-and-structure)
2. [Program Logic and Service Interactions](#2-program-logic-and-service-interactions)
3. [Data Services Architecture](#3-data-services-architecture)
4. [Development Setup and Usage](#4-development-setup-and-usage)
5. [Build and Deployment](#5-build-and-deployment)
6. [Design Decisions and Debugging](#6-design-decisions-and-debugging)

## 1. Project Architecture and Structure

The LLM Embeddings CPU solution follows a layered architecture with clear separation of concerns. The codebase is organized into five main projects, each serving a specific purpose in the overall system.

### 1.1 Solution Structure Overview

```
LlmEmbeddingsCpu/
├── src/
│   ├── LlmEmbeddingsCpu.App/          # Entry point and host configuration
│   ├── LlmEmbeddingsCpu.Core/         # Domain models and interfaces
│   ├── LlmEmbeddingsCpu.Common/       # Shared utilities and enums
│   ├── LlmEmbeddingsCpu.Data/         # Data persistence layer
│   └── LlmEmbeddingsCpu.Services/     # Business logic implementation
├── prerequisites/                      # Runtime dependencies
└── Output/                            # Build artifacts
```

### 1.2 Solution Architecture Diagram

The following diagram illustrates the dependency relationships between projects and the flow of data through the system:

```mermaid
graph TB
    %% Project Dependencies
    subgraph "LlmEmbeddingsCpu Solution"
        App[LlmEmbeddingsCpu.App<br/>📱 Entry Point & DI Container]
        Services[LlmEmbeddingsCpu.Services<br/>⚙️ Business Logic]
        Data[LlmEmbeddingsCpu.Data<br/>💾 Data Access Layer]
        Core[LlmEmbeddingsCpu.Core<br/>🔧 Domain Models & Interfaces]
        Common[LlmEmbeddingsCpu.Common<br/>📚 Shared Utilities]
    end
    
    %% Dependencies
    App --> Services
    App --> Data
    App --> Core
    App --> Common
    
    Services --> Data
    Services --> Core
    Services --> Common
    
    Data --> Core
    Data --> Common
    
    %% External Dependencies
    subgraph "External Dependencies"
        ONNX[ONNX Runtime<br/>🤖 ML Model Execution]
        WinAPI[Windows API<br/>🪟 System Hooks]
        FileSystem[File System<br/>📁 Local Storage]
    end
    
    Services --> ONNX
    Services --> WinAPI
    Data --> FileSystem
    
    %% Service Flow
    subgraph "Service Architecture"
        direction TB
        subgraph "Monitoring Services"
            KMS[KeyboardMonitorService]
            MMS[MouseMonitorService]
            WMS[WindowMonitorrService]
            RMS[ResourceMonitorService]
        end
        
        subgraph "Processing Services"
            CPS[ContinuousProcessingService]
            CRPS[CronProcessingService]
            IES[IntfloatEmbeddingService]
        end
        
        subgraph "Data Services"
            KLIS[KeyboardLogIOService]
            MLIS[MouseLogIOService]
            WLIS[WindowLogIOService]
            EIS[EmbeddingIOService]
            PSIS[ProcessingStateIOService]
        end
        
        subgraph "Aggregation"
            AS[AggregationService]
        end
    end
    
    %% Data Flow
    KMS --> KLIS
    MMS --> MLIS
    WMS --> WLIS
    RMS --> CPS
    
    CPS --> KLIS
    CPS --> IES
    CPS --> EIS
    CPS --> PSIS
    
    CRPS --> KLIS
    CRPS --> IES
    CRPS --> EIS
    CRPS --> PSIS
    
    AS --> KLIS
    AS --> MLIS
    AS --> WLIS
    AS --> EIS
    AS --> PSIS
    
    %% Launch Modes
    subgraph "Launch Modes"
        Logger[--logger<br/>🔍 Continuous Monitoring]
        Processor[--processor<br/>⚡ Resource-Aware Processing]
        CronProcessor[--cron-processor<br/>⏰ Scheduled Processing]
        Aggregator[--aggregator<br/>📦 Data Archiving]
    end
    
    Logger -.-> KMS
    Logger -.-> MMS
    Logger -.-> WMS
    Logger -.-> RMS
    
    Processor -.-> CPS
    CronProcessor -.-> CRPS
    Aggregator -.-> AS
    
    %% Styling
    classDef project fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef external fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef service fill:#e8f5e8,stroke:#1b5e20,stroke-width:2px
    classDef mode fill:#fff3e0,stroke:#e65100,stroke-width:2px
    
    class App,Services,Data,Core,Common project
    class ONNX,WinAPI,FileSystem external
    class KMS,MMS,WMS,RMS,CPS,CRPS,IES,KLIS,MLIS,WLIS,EIS,PSIS,AS service
    class Logger,Processor,CronProcessor,Aggregator mode
```

**Key Architecture Features:**

- **Clean Layered Architecture**: Clear separation of concerns with Core containing domain models, Data handling persistence, Services implementing business logic, and App orchestrating everything
- **Dependency Injection**: App layer configures and wires all services based on launch mode
- **Multi-Mode Operation**: Single application with four distinct operational modes, each with specific service configurations
- **No Circular Dependencies**: Clean dependency flow from App → Services → Data → Core/Common
- **External Integration**: Services layer handles Windows API hooks and ONNX model execution
- **File-Based Persistence**: Data layer abstracts file system operations with consistent naming and encryption

### 1.3 Project-by-Project Breakdown

#### LlmEmbeddingsCpu.App (Application Layer)
This is the entry point of the application. It contains:
- **Program.cs**: Configures dependency injection, logging, and determines which mode to run based on command-line arguments
- **deps/**: Contains the ONNX model files and tokenizer configurations
- Responsible for wiring up all services and starting the appropriate process based on launch mode

#### LlmEmbeddingsCpu.Core (Domain Layer)
Contains the core business entities and contracts that define the system's behavior:
- **Models/**:
  - `KeyboardInputLog.cs`: Represents keyboard input with timestamp, type, and content
  - `ActiveWindowLog.cs`: Captures window focus changes with title, handle, and process name
  - `MouseInputLog.cs`: Records mouse position, clicks, and scroll events
  - `Embedding.cs`: Stores generated text embeddings with associated metadata
- **Interfaces/**:
  - `IEmbeddingService.cs`: Contract for services that generate text embeddings
- **Enums/**:
  - `LaunchMode.cs`: Defines the four application modes (Logger, Processor, CronProcessor, Aggregator)
  - `KeyboardInputType.cs`: Distinguishes between printable text and special keys

#### LlmEmbeddingsCpu.Common (Shared Layer)
Provides utilities used throughout the application:
- **Extensions/**:
  - `StringExtensions.cs`: Contains utility method for ROT13 encryption for data obfuscation

#### LlmEmbeddingsCpu.Data (Data Access Layer)
Handles all file system operations and data persistence. Each IO service manages a specific type of data:
- **FileSystemIO/**:
  - `FileSystemIOService.cs`: Base service providing common file operations (read, write, move, delete)
- **KeyboardLogIO/**:
  - `KeyboardLogIOService.cs`: Manages keyboard log files with ROT13 encryption
- **WindowLogIO/**:
  - `WindowLogIOService.cs`: Handles window activity logs with encrypted window titles and process names
- **MouseLogIO/**:
  - `MouseLogIOService.cs`: Stores mouse activity without encryption
- **EmbeddingIO/**:
  - `EmbeddingIOService.cs`: Manages embedding storage in JSON format
- **ProcessingStateIO/**:
  - `ProcessingStateIOService.cs`: Tracks Embedding processing progress across application restarts

#### LlmEmbeddingsCpu.Services (Business Logic Layer)
Implements the core functionality through specialized services:
- **Monitoring Services/**:
  - `KeyboardMonitorService.cs`: Global keyboard hook implementation
  - `WindowMonitorrService.cs`: Windows API-based window monitoring (note the double 'r' - this naming avoids conflicts with global Windows API names)
  - `MouseMonitorService.cs`: Global mouse hook for tracking movements and clicks
  - `ResourceMonitorService.cs`: System resource monitoring and process launching
- **Processing Services/**:
  - `ContinuousProcessingService.cs`: Resource-aware batch processing
  - `CronProcessingService.cs`: Scheduled brute-force processing
  - `IntfloatEmbeddingService.cs`: ONNX-based embedding generation
- **Aggregation/**:
  - `AggregationService.cs`: Archive and housekeeping operations

## 2. Program Logic and Service Interactions

The application operates through four distinct launch modes, each serving a specific purpose in the data pipeline. These modes work together to create a robust system for capturing, processing, and archiving user activity data.

### 2.1 Application Launch Modes

The application determines its mode based on command-line arguments:
- `--logger`: Runs monitoring services continuously
- `--processor`: Performs resource-aware processing
- `--cron-processor`: Executes scheduled complete processing
- `--aggregator`: Archives completed data

### 2.2 Main Logger Process

The Logger mode is the primary continuous process that captures user activity.

```mermaid
graph TD
    A[Logger Process Start] --> B[Initialize Services]
    B --> C[KeyboardMonitorService]
    B --> D[MouseMonitorService]
    B --> E[WindowMonitorrService]
    B --> F[ResourceMonitorService]
    
    C --> G[Global Keyboard Hook]
    D --> H[Global Mouse Hook]
    E --> I[Window Change Detection]
    F --> J[CPU Usage Monitoring]
    
    G --> K[Buffer Keystrokes]
    K --> L[Flush to File on Special Key/Buffer Full]
    
    H --> M[Log Mouse Events]
    I --> N[Log Window Changes]
    
    J --> O{CPU < 30% for 3 checks?}
    O -->|Yes| P[Launch Processor]
    O -->|No| J
```

**Key Features:**
- Runs continuously as a scheduled task
- Captures all keyboard input with intelligent buffering (max 1,000 characters)
- Monitors window focus changes in real-time
- Tracks mouse movements and interactions
- Launches processor instances when system resources are available

**File Output:**
- `keyboard_logs-YYYYMMDD.txt`: ROT13-encrypted keyboard input
- `window_monitor_logs-YYYYMMDD.txt`: ROT13-encrypted window activity
- `mouse_logs-YYYYMMDD.txt`: Unencrypted mouse activity
- `application-logger-YYYYMMDD.log`: Application logs

### 2.3 Continuous Processor

The Processor mode handles opportunistic batch processing when system resources permit.

```mermaid
graph TD
    A[Processor Start] --> B[Load Processing State]
    B --> C[Get Unprocessed Dates]
    C --> D{Any Dates to Process?}
    D -->|No| E[Exit]
    D -->|Yes| F[Select Date]
    
    F --> G[Load Keyboard Logs]
    G --> H[Check Current Position]
    H --> I{More Logs?}
    I -->|No| J[Mark Date Complete]
    I -->|Yes| K{CPU < 80%?}
    
    K -->|No| L[Graceful Shutdown]
    K -->|Yes| M[Process Batch of 10]
    M --> N[Generate Embeddings]
    N --> O[Save Embeddings]
    O --> P[Update Processing State]
    P --> I
    
    J --> C
```

**Key Features:**
- Resource-aware processing with 80% CPU threshold
- Processes in batches of 10 logs for efficiency
- Maintains processing state for resumability
- Gracefully stops when resources are constrained
- Updates `processing_state.json` after each batch

**Processing Flow:**
1. Reads unprocessed keyboard logs
2. Generates embeddings using ONNX model
3. Stores embeddings in `embeddings/YYYYMMDD/{id}.json`
4. Updates processing progress

### 2.4 Cron Processor

The CronProcessor mode ensures complete processing during scheduled times.

```mermaid
graph TD
    A[CronProcessor Start] --> B[Load All Unprocessed Dates]
    B --> C{Any Dates?}
    C -->|No| D[Exit]
    C -->|Yes| E[Process Each Date]
    
    E --> F[Load All Logs for Date]
    F --> G[Skip Already Processed]
    G --> H[Generate All Embeddings]
    H --> I[Save All Embeddings]
    I --> J[Update Processing State]
    J --> K{More Dates?}
    K -->|Yes| E
    K -->|No| D
```

**Key Features:**
- No resource checking - processes everything
- Designed for off-hours execution (e.g., 3 AM)
- Ensures no logs are left unprocessed
- Handles large backlogs efficiently

### 2.5 Aggregator

The Aggregator mode performs housekeeping and prepares data for upload.

```mermaid
graph TD
    A[Aggregator Start] --> B[Find Completed Dates]
    B --> C{Any Complete?}
    C -->|No| D[Exit]
    C -->|Yes| E[Select Date]
    
    E --> F[Create Archive Structure]
    F --> G[upload-queue/hostname-user-YYYYMMDD/]
    G --> H[Create Subdirectories]
    H --> I[logs/]
    H --> J[embeddings/]
    
    I --> K[Move Log Files]
    J --> L[Move Embedding Files]
    
    K --> M{DEBUG Mode?}
    M -->|Yes| N[Move Keyboard Logs]
    M -->|No| O[Delete Keyboard Logs]
    
    L --> P[Remove from Processing State]
    P --> Q{More Dates?}
    Q -->|Yes| E
    Q -->|No| D
```

**Archive Structure:**
```
upload-queue/
└── COMPUTERNAME-USERNAME-YYYYMMDD/
    ├── logs/
    │   ├── window_monitor_logs.txt
    │   ├── mouse_logs.txt
    │   ├── keyboard_logs.txt (DEBUG only)
    │   └── application-*.log
    └── embeddings/
        └── YYYYMMDD/
            ├── {id1}.json
            ├── {id2}.json
            └── ...
```

### 2.6 Service Interaction Flow

The complete system operates as follows:

1. **Logger** continuously captures user activity
2. **ResourceMonitorService** monitors CPU usage and launches processors
3. **Processor** opportunistically processes logs when resources permit
4. **CronProcessor** ensures complete processing during scheduled times
5. **Aggregator** archives completed data for upload

## 3. Data Services Architecture

The Data layer provides a clean abstraction over file system operations, allowing the business logic to remain independent of storage implementation details. All services follow a consistent pattern and handle path management internally.

### 3.1 FileSystemIOService - The Foundation

All IO services depend on `FileSystemIOService`, which provides:

```csharp
public class FileSystemIOService
{
    private readonly string _basePath;
    
    // Core operations
    - EnsureDirectoryExists(string path)
    - ReadAllTextAsync(string filePath)
    - WriteAllTextAsync(string filePath, string content)
    - AppendAllTextAsync(string filePath, string content)
    - MoveFile(string source, string destination)
    - DeleteFile(string filePath)
    - CheckIfFileExists(string filePath)
    - GetFullPath(string relativePath)
}
```

**Base Path Resolution:**
- **Development**: Current working directory
- **Production**: `%LOCALAPPDATA%\LlmEmbeddingsCpu\` (user-specific, no admin rights required)

### 3.2 Data Encryption Strategy

The system uses ROT13 encryption for sensitive data to provide basic obfuscation:

#### Encrypted Data:
1. **Keyboard Logs** (`keyboard_logs-YYYYMMDD.txt`)
   - Format: `[HH:mm:ss] type|encrypted_content`
   - Example: `[14:23:45] Text|Uryyb Jbeyq` (Hello World)
   - Reason: Contains actual user input which could be sensitive

2. **Window Logs** (`window_monitor_logs-YYYYMMDD.txt`)
   - Format: `[HH:mm:ss] encrypted_title|handle|encrypted_process`
   - Example: `[14:23:45] Tbbtyr Puebzr|0x1234|puebzr.rkr`
   - Reason: Window titles may reveal private information

#### Unencrypted Data:
1. **Mouse Logs** (`mouse_logs-YYYYMMDD.txt`)
   - Format: `[HH:mm:ss] X|Y|button|clicks|delta`
   - Example: `[14:23:45] 1920|1080|Left|1|0`
   - Reason: Coordinates and click data have no meaningful content

### 3.3 Individual IO Services

#### KeyboardLogIOService
Manages keyboard input persistence with intelligent date handling:

```csharp
Key Methods:
- SaveLogAsync(KeyboardInputLog log)
- GetPreviousLogsAsyncDecrypted(DateTime date)
- GetDatesToProcess() // Returns dates with unprocessed logs
- GetFilePath(DateTime date) // Internal path management
```

**File Structure:**
- One file per day: `keyboard_logs-20240315.txt`
- Each line represents one keyboard event
- Automatic ROT13 encryption on save
- Automatic decryption on read

#### WindowLogIOService
Tracks window focus changes throughout the day:

```csharp
Key Methods:
- SaveLogAsync(ActiveWindowLog log)
- GetFilePath(DateTime date)
```

**Deduplication Logic:**
- Only logs when window focus actually changes
- Prevents duplicate entries for the same window

#### ProcessingStateIOService
Central state management for processing progress:

```csharp
Structure:
{
  "20240315": 1500,  // Date: ProcessedLineCount
  "20240316": 750,
  ...
}

Key Methods:
- GetProcessedCount(string dateKey)
- UpdateProcessedCount(string dateKey, int count)
- RemoveDate(string dateKey)
- SaveState() // Atomic save with temp file
```

**Features:**
- Atomic writes prevent corruption
- Enables resume after crashes
- Tracks progress per date
- Thread-safe operations

#### EmbeddingIOService
Manages the structured storage of generated embeddings:

```csharp
Directory Structure:
embeddings/
└── YYYYMMDD/
    ├── 00000000-0000-0000-0000-000000000001.json
    ├── 00000000-0000-0000-0000-000000000002.json
    └── ...

Key Methods:
- SaveEmbeddingAsync(Embedding embedding)
- SaveEmbeddingsBulkAsync(IEnumerable<Embedding> embeddings)
- GetEmbeddingsAsync(DateTime date)
- GetFolderPath(DateTime date)
```

### 3.4 Path Management Philosophy

All path management is encapsulated within IO services:
- Services never expose file paths to business logic
- All paths are computed internally based on dates or IDs
- Consistent naming conventions across all services
- Automatic directory creation when needed

This abstraction allows for:
- Easy migration to different storage mechanisms
- Consistent file organization
- Simplified testing with mock implementations
- Clear separation of concerns

## 4. Development Setup and Usage

This section covers everything needed to run the application in development mode for testing and debugging.

### 4.1 Development Mode Overview

Development mode provides several advantages:
- Local file storage in the current directory
- Enhanced logging output
- Keyboard log files are preserved (not deleted)
- No administrative privileges required
- Easy debugging with Visual Studio or VS Code

### 4.2 Prerequisites

1. **.NET 9.0 SDK** or later
2. **Windows 10/11** (required for Windows hooks)
3. **ONNX Model Files** (see model setup below)
4. **Visual Studio 2022** or **VS Code** (optional but recommended)

### 4.3 Model Setup

The embedding service requires the ONNX model file which is too large for version control:

1. Navigate to: `src/LlmEmbeddingsCpu.App/deps/intfloat/multilingual-e5-small/`
2. Ensure these files exist:
   - `config.json` ✓ (in repository)
   - `special_tokens_map.json` ✓ (in repository)
   - `tokenizer.json` ✓ (in repository)
   - `tokenizer_config.json` ✓ (in repository)
   - `model.onnx` ❌ (must be added manually)

3. Download `model.onnx` from: https://olli-master-thesis.s3.eu-west-1.amazonaws.com/multilingual-e5-small-onnx.zip

### 4.4 Running in Development Mode

#### Basic Development Commands:

```bash
# Run Logger mode (monitors user activity)
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -- --logger

# Run Processor mode (processes logs)
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -- --processor

# Run CronProcessor mode (force processes all)
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -- --cron-processor

# Run Aggregator mode (archives completed data)
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -- --aggregator
```

#### Architecture-Specific Commands:

```bash
# Force x64 architecture
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -r win-x64 -- --logger

# Force ARM64 architecture
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -r win-arm64 -- --logger
```

### 4.5 Development File Locations

In development mode, all files are stored under the build output directory for your target architecture and configuration. For example:

- **ARM64 Debug:**  
  `./src/LlmEmbeddingsCpu.App/bin/Debug/net9.0-windows/win-arm64/logs/`
- **x64 Debug:**  
  `./src/LlmEmbeddingsCpu.App/bin/Debug/net9.0-windows/win-x64/logs/`
- (Release builds use `bin/Release/...` accordingly.)

**Directory structure inside `logs/`:**
```
logs/
├── embeddings/
│   └── YYYYMMDD/
│       └── <guid>.json
├── upload-queue/
│   └── <hostname>-<username>-YYYYMMDD.json/
│       └── 
├── application-aggregator-YYYYMMDD.log
├── application-cronprocessor-YYYYMMDD.log
├── application-logger-YYYYMMDD.log
├── application-processor-YYYYMMDD.log
├── processing_state.json
├── keyboard_logs-YYYYMMDD.txt
├── mouse_logs-YYYYMMDD.txt
└── window_monitor_logs-YYYYMMDD.txt
```
- The `embeddings/` folder contains daily subfolders with individual embedding JSON files.
- The `upload-queue/` folder may be empty during development.
- Log and data files are created per day.

> **Note:** The exact path will differ depending on your build configuration (Debug/Release) and target architecture (x64/arm64).

### 4.6 Debug vs Release Behavior

The application behaves differently based on the build configuration:

#### Debug Mode (`-c Debug` or default):
```csharp
#if DEBUG
    // Keyboard logs are MOVED to archive
    _fileSystemIOService.MoveFile(keyboardLogFilePath, destinationPath);
    
    // Extra logging information
    _logger.LogDebug("Detailed processing information...");
    
    // Additional validation checks
    ValidateEmbeddings(embeddings);
#endif
```

Additionally, the application logs a lot more when run in Debug mode.

#### Release Mode (`-c Release`):
```csharp
#if !DEBUG
    // Keyboard logs are DELETED after processing
    _fileSystemIOService.DeleteFile(keyboardLogFilePath);
    
    // Minimal logging
    // No validation overhead
#endif
```

### 4.7 Typical Development Workflow

1. **Start Logger** to begin capturing activity:
   ```bash
   dotnet run --project src/LlmEmbeddingsCpu.App --logger
   ```

2. **Generate some activity** (type, move mouse, switch windows)

   > **Note:** The processor will generally start automatically when the CPU usage is below 30% for 9 minutes in a row. This is handled by the ResourceMonitor. You can also trigger processing manually at any time with:
   > ```bash
   > dotnet run --project src/LlmEmbeddingsCpu.App --processor
   > ```
   > The processor will stop once all files are processed, or if CPU usage rises above 80%.

3. **Force full processing (optional):**
   If you want to process all available logs immediately, regardless of CPU usage, you can run:
   ```bash
   dotnet run --project src/LlmEmbeddingsCpu.App --cron-processor
   ```
   This will process all pending logs in one go, ignoring resource checks.

4. **Check generated files**:
   - Review logs in the output directory
   - Verify embeddings in `embeddings/` folder
   - Check `processing_state.json` for progress

5. **Run Aggregator** to archive:
   ```bash
   dotnet run --project src/LlmEmbeddingsCpu.App --aggregator
   ```

6. **Verify archive** in `upload-queue/` directory

## 5. Build and Deployment

This section covers the complete deployment pipeline from building the application to creating the installer and configuring Windows Task Scheduler.

### 5.1 .NET Build Fundamentals

#### Build Configurations

.NET supports two primary build configurations that affect code behavior:

**Debug Configuration:**
- Includes debugging symbols
- No optimization
- `DEBUG` conditional compilation symbol defined
- Larger file size
- Better stack traces for debugging

**Release Configuration:**
- Optimized code
- No debugging symbols (unless specified)
- `DEBUG` symbol not defined
- Smaller file size
- Better performance

#### Publishing Options

**.NET Publishing Models:**

1. **Framework-Dependent**: Requires .NET runtime on target machine
2. **Self-Contained**: Includes .NET runtime with application
3. **Single File**: Bundles everything into one executable

We use **Self-Contained Single File** deployment for simplicity:
- No runtime prerequisites for end users
- Single executable for easy distribution
- All dependencies bundled inside
- Simplified installer creation

### 5.2 Building the Application

#### Build Commands for Production:

**x64 Architecture:**
```bash
dotnet publish src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj \
  -c Release \
  -r win-x64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:PublishTrimmed=true \
  -p:DebugType=None \
  -p:DebugSymbols=false
```

**ARM64 Architecture:**
```bash
dotnet publish src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj \
  -c Release \
  -r win-arm64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:PublishTrimmed=true \
  -p:DebugType=None \
  -p:DebugSymbols=false
```

**Build Parameters Explained:**
- `-c Release`: Sets Release configuration (disables DEBUG)
- `-r win-x64/win-arm64`: Runtime identifier for target architecture
- `-p:PublishSingleFile=true`: Creates single executable
- `-p:SelfContained=true`: Includes .NET runtime
- `-p:IncludeAllContentForSelfExtract=true`: Includes all content files
- `-p:PublishTrimmed=true`: Removes unused code
- `-p:DebugType=None`: No debug information
- `-p:DebugSymbols=false`: No PDB files

**Output Locations:**
- x64: `src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-x64\publish\LlmEmbeddingsCpu.App.exe`
- ARM64: `src\LlmEmbeddingsCpu.App\bin\Release\net9.0-windows\win-arm64\publish\LlmEmbeddingsCpu.App.exe`

### 5.3 Impact of Release Configuration

The `-c Release` flag affects code behavior through conditional compilation:

```csharp
// In Release mode, this code is NOT included:
#if DEBUG
    _logger.LogDebug("Detailed debug information");
    _fileSystemIOService.MoveFile(keyboardLogPath, archivePath);
#else
    // This code runs in Release mode:
    _fileSystemIOService.DeleteFile(keyboardLogPath);
#endif
```

**Key Differences in Release Mode:**
1. Keyboard logs are deleted after processing (privacy)
2. Reduced logging verbosity
3. No debug assertions
4. Optimized performance

### 5.4 Production File Storage

In production, files are stored in user-specific AppData:

**Base Path:** `%LOCALAPPDATA%\LlmEmbeddingsCpu\`

Expands to: `C:\Users\{USERNAME}\AppData\Local\LlmEmbeddingsCpu\`

**Why AppData/Local?**
- User-specific (each user has separate data)
- No administrator privileges required
- Automatically backed up with user profile
- Hidden from casual browsing
- Standard location for application data

**Production Directory Structure:**
```
C:\Users\{USERNAME}\AppData\Local\LlmEmbeddingsCpu\
├── keyboard_logs-YYYYMMDD.txt
├── window_monitor_logs-YYYYMMDD.txt
├── mouse_logs-YYYYMMDD.txt
├── processing_state.json
├── embeddings\
│   └── YYYYMMDD\
│       └── {guid}.json
├── upload-queue\
│   └── COMPUTERNAME-USERNAME-YYYYMMDD\
└── application-*.log
```

### 5.5 Application vs Installer Executable

**Two Different Executables:**

1. **Application Executable** (`LlmEmbeddingsCpu.App.exe`)
   - The actual program that does the work
   - Created by `dotnet publish`
   - Can be run directly with command-line arguments
   - Installed to: `C:\Program Files\LLM Embeddings CPU\`

2. **Installer Executable** (`LlmEmbeddingsCpuInstallerX64.exe`)
   - Created by Inno Setup
   - Wraps the application executable
   - Handles installation process
   - Creates scheduled tasks
   - Installs prerequisites

### 5.6 Inno Setup Configuration (.iss files)

The `.iss` files define how the installer behaves:

```pascal
[Setup]
AppName=LLM Embeddings CPU
DefaultDirName={pf}\LLM Embeddings CPU
OutputDir=Output
Compression=lzma2
SolidCompression=yes

[Files]
Source: "src\...\publish\LlmEmbeddingsCpu.App.exe"; DestDir: "{app}"
Source: "prerequisites\VC_redist.x64.exe"; DestDir: "{tmp}"

[Run]
; Install Visual C++ Redistributable if needed
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/quiet /norestart"

; Create scheduled tasks
Filename: "schtasks.exe"; Parameters: "/Create /F /RL HIGHEST /SC ONLOGON /DELAY 0000:10 /TN ""LLMEmbeddingsCpuLogger"" /TR ""'{app}\LlmEmbeddingsCpu.App.exe' --logger"" /IT"
```

### 5.7 Task Scheduler Configuration

The installer creates three scheduled tasks:

#### 1. Logger Task (`LLMEmbeddingsCpuLogger`)
```cmd
schtasks /Create /F /RL HIGHEST /SC ONLOGON /DELAY 0000:10 
         /TN "LLMEmbeddingsCpuLogger" 
         /TR "'C:\Program Files\LLM Embeddings CPU\LlmEmbeddingsCpu.App.exe' --logger" 
         /IT
```

**Parameters:**
- `/RL HIGHEST`: Run with highest privileges (required for global hooks)
- `/SC ONLOGON`: Trigger on user login
- `/DELAY 0000:10`: Wait 10 seconds after login
- `/IT`: Allow interactive (can interact with desktop)

**PowerShell Configuration:**
```powershell
$task = Get-ScheduledTask -TaskName "LLMEmbeddingsCpuLogger"
$task.Settings.DisallowStartIfOnBatteries = $false
$task.Settings.StopIfGoingOnBatteries = $false
$task.Settings.ExecutionTimeLimit = 'PT0S'  # No time limit
Set-ScheduledTask -TaskName "LLMEmbeddingsCpuLogger" -Settings $task.Settings
```

#### 2. Cron Processor Task (`LLMEmbeddingsCpuCron`)
```cmd
schtasks /Create /F /RL HIGHEST /SC DAILY /ST 00:00 \
         /TN "LLMEmbeddingsCpuCron" \
         /TR "'C:\Program Files\LLM Embeddings CPU\LlmEmbeddingsCpu.App.exe' --cron-processor"
```

**Schedule:** Daily at midnight (00:00)
**Purpose:** Complete processing of all pending logs

#### 3. Aggregator Task (`LLMEmbeddingsCpuAggregator`)
```cmd
schtasks /Create /F /RL HIGHEST /SC HOURLY \
         /TN "LLMEmbeddingsCpuAggregator" \
         /TR "'C:\Program Files\LLM Embeddings CPU\LlmEmbeddingsCpu.App.exe' --aggregator"
```

**Schedule:** Hourly
**Purpose:** Archive completed data

### 5.8 Building the Installer

1. **Prerequisites:**
   - Install Inno Setup Compiler from https://jrsoftware.org/isinfo.php
   - Ensure VC++ Redistributables are in `prerequisites/` folder

2. **Compile Steps:**
   - Open Inno Setup Compiler
   - File → Open → Select `LlmEmbeddingsCpuInstallerX64.iss`
   - Build → Compile (or press F9)

3. **Output:**
   - Installer created in `Output/LlmEmbeddingsCpuInstallerX64.exe`
   - Ready for distribution

### 5.9 Installation Process

When the installer runs:

1. **Extracts Files** to `C:\Program Files\LLM Embeddings CPU\`
2. **Checks/Installs** Visual C++ Redistributable
3. **Creates Scheduled Tasks** for Logger, Cron, and Aggregator
4. **Configures Power Settings** for laptop compatibility
5. **Starts Logger Task** immediately

### 5.10 Uninstallation

The uninstaller:
1. Stops all running tasks
2. Deletes scheduled tasks
3. Removes program files
4. Does NOT delete user data in AppData so logs are preserved

## 6. Design Decisions and Debugging

This section explains key architectural decisions and provides debugging strategies for common issues.

### 6.1 Why Scheduled Tasks Instead of Windows Services

**The Constraint:** Windows Services run in Session 0, isolated from user sessions. They cannot:
- Access user-level keyboard/mouse hooks
- Interact with user desktop
- See user-specific window information

**The Solution:** Scheduled tasks run in the user's session, allowing:
- Global keyboard and mouse hooks
- Window title monitoring
- User-specific data access
- Proper interaction with desktop applications

### 6.2 Manual Task Management

#### Viewing Tasks via Command Line:

```cmd
# List all LLM Embeddings tasks
schtasks /Query /TN "LLMEmbeddingsCpu*" /V /FO LIST

# Detailed view of specific task
schtasks /Query /TN "LLMEmbeddingsCpuLogger" /V /FO LIST

# Check task status
schtasks /Query /TN "LLMEmbeddingsCpuLogger" /FO CSV | findstr "Status"
```

#### Common Task Commands:

```cmd
# Run task immediately
schtasks /Run /TN "LLMEmbeddingsCpuLogger"

# Stop running task
schtasks /End /TN "LLMEmbeddingsCpuLogger"

# Disable task
schtasks /Change /TN "LLMEmbeddingsCpuLogger" /DISABLE

# Enable task
schtasks /Change /TN "LLMEmbeddingsCpuLogger" /ENABLE

# Delete task
schtasks /Delete /TN "LLMEmbeddingsCpuLogger" /F
```

### 6.3 Debugging Deployment Issues

#### Common Problem: Application Crashes After Deployment

**Symptoms:**
- Task shows as "Last Run Result: 0x1" or other error code
- No log files created
- Process exits immediately

**Debugging Steps:**

1. **Check Task Scheduler History:**
   - Open Task Scheduler
   - Find task in Library
   - Click "History" tab
   - Look for error events

2. **Decode Error Codes:**
   Task Scheduler returns errors in hexadecimal. Common codes:
   
   ```powershell
   # Convert hex to decimal
   [Convert]::ToInt32("0x1", 16)  # Returns 1
   
   # Look up error
   net helpmsg 1  # "Incorrect function"
   ```
   
   **Common Error Codes:**
   - `0x1` (1): General failure
   - `0x2` (2): File not found
   - `0x5` (5): Access denied
   - `0x8007000E` (-2147024882): Out of memory
   - `0x80070032` (-2147024846): Not supported

3. **Enable Console for Debugging:**
   Temporarily modify the task to keep console open:
   ```cmd
   schtasks /Change /TN "LLMEmbeddingsCpuLogger" 
            /TR "cmd /k 'C:\Program Files\LLM Embeddings CPU\LlmEmbeddingsCpu.App.exe' --logger"
   ```

### 6.4 The DLL Dependency Problem

**The Issue:** `System.DllNotFoundException` for `hf_tokenizers.dll`

**Root Cause:** Missing Visual C++ Runtime (`VCRUNTIME140.dll`)

**Diagnosis with Process Monitor:**

1. **Download Process Monitor** (procmon.exe) from Microsoft Sysinternals

2. **Configure Filter:**
   - Filter → Filter... (Ctrl+L)
   - Add: `Process Name` → `is` → `LlmEmbeddingsCpu.App.exe` → `Include`
   - Add: `Result` → `is` → `NAME NOT FOUND` → `Include`

3. **Capture and Analyze:**
   ```
   Example Output:
   LlmEmbeddingsCpu.App.exe | CreateFile | C:\Windows\System32\VCRUNTIME140.dll | NAME NOT FOUND
   LlmEmbeddingsCpu.App.exe | CreateFile | C:\Windows\SysWOW64\VCRUNTIME140.dll | NAME NOT FOUND
   ```

4. **Solution:** Installer now includes VC++ Redistributable