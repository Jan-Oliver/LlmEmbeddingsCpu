Of course. Here is a comprehensive set of instructions for a junior software engineer to implement the new architecture. This document covers the complete plan, including all refinements, and is designed to be followed step-by-step.

---

### **Technical Specification & Implementation Plan: LlmEmbeddingsCpu V2**

**Project Goal:** To refactor the existing application from a single monolithic process into a robust, multi-part system. This new architecture will enable continuous, resource-aware log processing during the day while maintaining a nightly catch-all process and a separate housekeeping/archival process. The key principle is **Process Isolation** to ensure the stability of the critical logging components.

#### **Phase 1: Core Application Refactoring**

This phase lays the groundwork for all other modules.

1.  **Implement Four Launch Modes in `Program.cs`:**
    * Modify the `Main` method to parse four distinct command-line arguments: `--logger`, `--processor`, `--cron-processor`, and `--aggregator`.
    * Use an `if/else if` structure to create a unique execution path for each mode.

2.  **Implement Conditional Dependency Injection:**
    * Update the `ConfigureServices` method to accept the current launch mode.
    * Based on the mode, register only the necessary services. For example:
        * `--logger`: Registers all monitoring services (`KeyboardMonitorService`, etc.) and the new `ResourceMonitorService`.
        * `--processor`: Registers the new `ContinuousProcessingService` and its dependencies (`EmbeddingStorageService`, etc.).
        * `--cron-processor`: Registers the new `NightlyCronProcessingService`.
        * `--aggregator`: Registers the new `AggregationService`.

3.  **Create a `CrossProcessLockingService`:**
    * This is a **critical, mandatory** service for preventing data corruption.
    * Implement this service using a system-wide `System.Threading.Mutex`.
    * Give the Mutex a unique, constant name (e.g., `Global\\LlmEmbeddingsCpuProcessingMutex`).
    * Expose simple `AcquireLock()` and `ReleaseLock()` methods. This service will be used by the `--processor`, `--cron-processor`, and `--aggregator` processes.

---

#### **Phase 2: Module 1 - The Logger (`--logger`)**

This is the always-on "watchdog" process.

1.  **Create the `ResourceMonitorService` Class:**
    * This service will be hosted exclusively within the `--logger` process.
    * It will use a `System.Timers.Timer` to trigger checks periodically (e.g., every 3 minutes).

2.  **Implement the Watchdog Logic:**
    * In the timer's elapsed event handler, perform two checks:
        1.  **System Resources:** Use `System.Diagnostics.PerformanceCounter` to check if CPU usage has been below a configurable threshold (e.g., 30%) for a sustained period (e.g., 3 consecutive checks).
        2.  **Check for Work:** Scan the log directories. For each log file, compare its line count on disk against the number of processed lines recorded in `processing_state.json`. If `lines_on_disk > lines_in_state`, there is work to do.
    * If **both** conditions are met, use `System.Diagnostics.Process.Start` to launch a new instance of the application with the `--processor` argument. Ensure you check that a `--processor` instance isn't already running before launching a new one.

---

#### **Phase 3: Module 2 - The Continuous Processor (`--processor`)**

This is the opportunistic batch processor.

1.  **Create the `ContinuousProcessingService` Class:**
    * This service will contain the core embedding logic.
    * It will use the existing `EmbeddingStorageService` to save generated embeddings.

2.  **Define and Use `processing_state.json`:**
    * This JSON file will act as the single source of truth for processing progress. It will map full log file paths to the integer count of processed lines.

3.  **Implement the Graceful Shutdown Loop (Exact Sequence):**
    * This is the core logic for the service. It must be implemented precisely as follows:
        1.  On startup, **acquire the lock** from `CrossProcessLockingService`. If the lock cannot be acquired, exit immediately.
        2.  Identify the oldest log file with unprocessed lines.
        3.  Enter a `while` loop that continues as long as there are unprocessed lines in the target file.
        4.  **Inside the loop (at the top):** Perform a resource check using `PerformanceCounter`.
        5.  **If resources are insufficient** (e.g., CPU > 80%), `break` the loop and proceed to the final step.
        6.  **If resources are sufficient,** process the next batch of 10 lines.
        7.  After the batch is successfully processed, **update `processing_state.json`** with the new line count. This is a critical step to ensure state is not lost.
        8.  The loop repeats.
        9.  After the loop concludes (for any reason), **release the lock** from `CrossProcessLockingService` and let the process exit.

---

#### **Phase 4: Module 3 - The Nightly Cron (`--cron-processor`)**

This is the brute-force "catch-all" to guarantee completion.

1.  **Create the `NightlyCronProcessingService` Class:**
    * This service will share logic with the `ContinuousProcessingService`.
    * **Key Difference:** Its processing loop will not perform any resource checks. It will run until every single line in every single log file has been processed.
    * It must acquire and release the `CrossProcessLockingService` lock.

---

#### **Phase 5: Module 4 - The Aggregator (`--aggregator`)**

This is the housekeeping and archival process.

1.  **Create the `AggregationService` Class:**
    * Refactor the logic from the old `UploadData` method into this service.
    * It must acquire and release the `CrossProcessLockingService` lock.

2.  **Implement the Completion Check Logic:**
    * The service will iterate through log files from **past days only**.
    * For each file, it will determine completion by:
        1.  Counting the total lines in the log file on disk.
        2.  Reading the number of processed lines from `processing_state.json`.
        3.  If `lines_on_disk == lines_in_state`, the file is complete.
    * For completed days, move the log files and embeddings to the archive and remove their entries from `processing_state.json`.

---

#### **Phase 6: Deployment & Task Scheduler Configuration**

The Inno Setup installer script must be updated to create three distinct scheduled tasks.

1.  **Task 1: Logger**
    * **Command:** `LlmEmbeddingsCpu.App.exe --logger`
    * **Trigger:** On user logon.
    * **Settings:** Run continuously with highest privileges. Remove any limitations like not power supply and run on battery power.

2.  **Task 2: Nightly Cron**
    * **Command:** `LlmEmbeddingsCpu.App.exe --cron-processor`
    * **Trigger:** Daily at midnight (00:00).
    * **Settings:** Must have **"Wake the computer to run this task"** enabled and be configured to run on battery power.

3.  **Task 3: Aggregator**
    * **Command:** `LlmEmbeddingsCpu.App.exe --aggregator`
    * **Trigger:** On an hourly schedule.