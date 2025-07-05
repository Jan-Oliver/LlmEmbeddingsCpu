using LlmEmbeddingsCpu.Data.FileSystemIO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using LlmEmbeddingsCpu.Data.KeyboardLogIO;

namespace LlmEmbeddingsCpu.Services.ResourceMonitor
{
    /// <summary>
    /// Monitors system resources and triggers continuous processing when conditions are met.
    /// </summary>
    public class ResourceMonitorService : IDisposable
    {
        private readonly ILogger<ResourceMonitorService> _logger;
        private readonly FileSystemIOService _fileSystemIOService;
        private readonly KeyboardLogIOService _keyboardLogIOService;

        private readonly System.Timers.Timer _monitoringTimer;
        private readonly List<float> _cpuUsageHistory = new();
        private readonly PerformanceCounter _cpuCounter;
        private bool _disposed = false;

        private const int MonitoringIntervalMs = 180000; // 3 minutes
        private const float CpuThreshold = 30.0f; // 30%
        private const int RequiredLowCpuChecks = 3;
        private const string ProcessingStatePath = "processing_state.json";

        public ResourceMonitorService(
            ILogger<ResourceMonitorService> logger, 
            FileSystemIOService fileSystemIOService,
            KeyboardLogIOService keyboardLogIOService
        ) {
            _logger = logger;
            _fileSystemIOService = fileSystemIOService;
            _keyboardLogIOService = keyboardLogIOService;

            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            
            // Initialize timer
            _monitoringTimer = new System.Timers.Timer(MonitoringIntervalMs);
            _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
            _monitoringTimer.AutoReset = true;
        }

        /// <summary>
        /// Starts the resource monitoring process.
        /// </summary>
        public void StartMonitoring()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ResourceMonitorService));

            _logger.LogInformation("Starting resource monitoring with {IntervalMs}ms interval", MonitoringIntervalMs);
            
            // Take initial CPU reading (first reading is usually inaccurate)
            _cpuCounter.NextValue();
            
            _monitoringTimer.Start();
        }

        /// <summary>
        /// Stops the resource monitoring process.
        /// </summary>
        public void StopMonitoring()
        {
            _monitoringTimer?.Stop();
            _logger.LogInformation("Resource monitoring stopped");
        }

        private void OnMonitoringTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                CheckResourcesAndTriggerProcessing();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource monitoring check");
            }
        }

        private void CheckResourcesAndTriggerProcessing()
        {
            // Check CPU usage
            var currentCpuUsage = _cpuCounter.NextValue();
            _cpuUsageHistory.Add(currentCpuUsage);

            // Keep only the last required number of checks
            if (_cpuUsageHistory.Count > RequiredLowCpuChecks)
            {
                _cpuUsageHistory.RemoveAt(0);
            }

            _logger.LogDebug("Current CPU usage: {CpuUsage:F2}%, History count: {HistoryCount}", 
                currentCpuUsage, _cpuUsageHistory.Count);

            // Check if we have enough history and all values are below threshold
            if (_cpuUsageHistory.Count >= RequiredLowCpuChecks && 
                _cpuUsageHistory.All(usage => usage < CpuThreshold))
            {
                _logger.LogDebug("CPU usage consistently below {Threshold}% for {Checks} checks", 
                    CpuThreshold, RequiredLowCpuChecks);

                // Check for work
                if (HasUnprocessedWork())
                {
                    _logger.LogInformation("Unprocessed work detected, triggering continuous processor");
                    TriggerContinuousProcessor();
                }
                else
                {
                    _logger.LogDebug("No unprocessed work found");
                }
            }
            else
            {
                _logger.LogDebug("CPU usage not consistently low enough to trigger processing");
            }
        }

        private bool HasUnprocessedWork()
        {
            try
            {
                var processingState = LoadProcessingState();
                
                // Check all log files for unprocessed lines
                var logFiles = GetLogFiles();
                
                foreach (var logFile in logFiles)
                {
                    var linesOnDisk = CountLinesInFile(logFile);
                    var processedLines = processingState.GetValueOrDefault(logFile, 0);
                    
                    if (linesOnDisk > processedLines)
                    {
                        _logger.LogDebug("Found work in {LogFile}: {LinesOnDisk} lines on disk, {ProcessedLines} processed", 
                            logFile, linesOnDisk, processedLines);
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for unprocessed work");
                return false;
            }
        }

        private Dictionary<string, int> LoadProcessingState()
        {
            try
            {
                var stateFilePath = _fileSystemIOService.GetFullPath(ProcessingStatePath);
                
                if (!File.Exists(stateFilePath))
                {
                    return new Dictionary<string, int>();
                }

                var json = File.ReadAllText(stateFilePath);
                return JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading processing state, returning empty state");
                return new Dictionary<string, int>();
            }
        }

        private List<string> GetLogFiles()
        {
            var logFiles = new List<string>();
            
            // Get keyboard log files
            var datesToProcess = _keyboardLogIOService.GetDatesToProcess();
            foreach (var date in datesToProcess)
            {
                logFiles.Add(_keyboardLogIOService.GetFilePath(date));
            }
            
            return logFiles;
        }

        private int CountLinesInFile(string filePath)
        {
            try
            {
                if (!_fileSystemIOService.CheckIfFileExists(filePath))
                    return 0;

                var content = _fileSystemIOService.ReadFileIfExists(filePath);
                return content.Split('\n').Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting lines in file {FilePath}", filePath);
                return 0;
            }
        }

        private void TriggerContinuousProcessor()
        {
            try
            {
                // Check if processor is already running
                if (IsProcessorRunning())
                {
                    _logger.LogInformation("Continuous processor or cron processor is already running, skipping trigger");
                    return;
                }

                var currentExecutable = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExecutable))
                {
                    _logger.LogError("Could not determine current executable path");
                    return;
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = currentExecutable,
                    Arguments = "--processor",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(processStartInfo);
                _logger.LogInformation("Launched continuous processor with PID {ProcessId}", process?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching continuous processor");
            }
        }

        private bool IsProcessorRunning()
        {
            try
            {
                var currentProcessName = Process.GetCurrentProcess().ProcessName;
                var allProcesses = Process.GetProcessesByName(currentProcessName);
                
                // Check if any process has --processor argument
                foreach (var process in allProcesses)
                {
                    try
                    {
                        var commandLine = GetProcessCommandLine(process);
                        if (!string.IsNullOrEmpty(commandLine) && 
                            (commandLine.Contains("--processor") || 
                            commandLine.Contains("--cron-processor"))
                        )
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Ignore errors when checking individual processes
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if processor is running");
                return false;
            }
        }

        private string GetProcessCommandLine(Process process)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
                
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    return obj["CommandLine"]?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return string.Empty;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _monitoringTimer?.Dispose();
            _cpuCounter?.Dispose();
            _disposed = true;
        }
    }
}