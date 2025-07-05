using LlmEmbeddingsCpu.Data.EmbeddingIO;
using LlmEmbeddingsCpu.Data.FileSystemIO;
using LlmEmbeddingsCpu.Data.KeyboardLogIO;
using LlmEmbeddingsCpu.Data.MouseLogIO;
using LlmEmbeddingsCpu.Data.ProcessingStateIO;
using LlmEmbeddingsCpu.Data.WindowLogIO;
using LlmEmbeddingsCpu.Common.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LlmEmbeddingsCpu.Services.Aggregation
{
    /// <summary>
    /// Provides housekeeping and archival services for processed log files.
    /// </summary>
    public class AggregationService(
        ILogger<AggregationService> logger,
        FileSystemIOService fileSystemIOService,
        EmbeddingIOService embeddingIOService,
        KeyboardLogIOService keyboardLogIOService,
        MouseLogIOService mouseLogIOService,
        WindowLogIOService windowLogIOService,
        ProcessingStateIOService processingStateIOService
    )
    {
        private readonly ILogger<AggregationService> _logger = logger;
        private readonly FileSystemIOService _fileSystemIOService = fileSystemIOService;
        private readonly EmbeddingIOService _embeddingIOService = embeddingIOService;
        private readonly KeyboardLogIOService _keyboardLogIOService = keyboardLogIOService;
        private readonly MouseLogIOService _mouseLogIOService = mouseLogIOService;
        private readonly WindowLogIOService _windowLogIOService = windowLogIOService;
        private readonly ProcessingStateIOService _processingStateIOService = processingStateIOService;

        private const string UploadPath = "upload-queue";

        /// <summary>
        /// Starts the aggregation service to archive completed log files.
        /// </summary>
        public async Task StartAggregationAsync()
        {
            _logger.LogInformation("Starting aggregation service");

            try
            {
                var completedDates = await GetCompletedDatesAsync();

                _logger.LogInformation("Found {CompletedCount} completed dates to aggregate", completedDates.Count);

                foreach (var date in completedDates)
                {
                    await AggregateCompletedDate(date);
                }

                _logger.LogInformation("Aggregation service completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during aggregation");
            }
        }

        private async Task<List<DateTime>> GetCompletedDatesAsync()
        {
            var completedDates = new List<DateTime>();
            var today = DateTime.Today;

            try
            {
                // Get all available dates from past days only using the storage service
                var availableDates = _keyboardLogIOService.GetDatesToProcess()
                    .Where(date => date.Date < today)
                    .ToList();

                foreach (var date in availableDates)
                {
                    var dateKey = ProcessingStateIOService.GetDateKey(date);
                    var processedCount = _processingStateIOService.GetProcessedCount(dateKey);
                    if (await IsDateCompleteAsync(date, processedCount))
                    {
                        completedDates.Add(date);
                        _logger.LogDebug("Date {Date} marked as completed", dateKey);
                    }
                    else
                    {
                        _logger.LogDebug("Date {Date} is not yet completed", dateKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining completed dates");
            }

            return completedDates.OrderBy(d => d).ToList();
        }


        private async Task<bool> IsDateCompleteAsync(DateTime date, int processedCount)
        {
            try
            {
                var logs = await _keyboardLogIOService.GetPreviousLogsAsyncDecrypted(date);
                var totalCount = logs.Count();
                
                var isComplete = totalCount == processedCount;
                
                _logger.LogDebug("Date {Date}: {TotalCount} logs total, {ProcessedCount} processed, Complete: {IsComplete}", 
                    date.ToString("yyyyMMdd"), totalCount, processedCount, isComplete);
                
                return isComplete;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking completion status for date {Date}", date.ToString("yyyyMMdd"));
                return false;
            }
        }

        private async Task AggregateCompletedDate(DateTime date)
        {
            try
            {
                _logger.LogInformation("Aggregating completed date: {Date}", date.ToString("yyyyMMdd"));
                
                // Create combined directory structure
                var hostname = Environment.MachineName;
                var userId = Environment.UserName;
                var dateStr = date.ToString("yyyyMMdd");
                var combinedPath = Path.Combine(UploadPath, $"{hostname}-{userId}-{dateStr}");
                var logsPath = Path.Combine(combinedPath, "logs");
                var embeddingsPath = Path.Combine(combinedPath, "embeddings");
                
                _logger.LogInformation("Combined path: {CombinedPath}", combinedPath);
                _fileSystemIOService.EnsureDirectoryExists(combinedPath);
                _fileSystemIOService.EnsureDirectoryExists(logsPath);
                _fileSystemIOService.EnsureDirectoryExists(embeddingsPath);

                // Move all log files to logs subfolder
                await MoveWindowLogs(date, logsPath);
                await MoveMouseLogs(date, logsPath);
                await MoveKeyboardLogs(date, logsPath);
                await MoveApplicationLogs(date, logsPath);

                // Move embeddings folder
                await MoveEmbeddings(date, embeddingsPath);

                _logger.LogInformation("Successfully aggregated date: {Date}", date.ToString("yyyyMMdd"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating date {Date}", date.ToString("yyyyMMdd"));
            }
        }

        private async Task MoveWindowLogs(DateTime date, string logsPath)
        {
            try
            {
                var windowMonitorLogFilePath = _windowLogIOService.GetFilePath(date);
                var windowMonitorLogExists = _fileSystemIOService.CheckIfFileExists(windowMonitorLogFilePath);
                
                if (windowMonitorLogExists)
                {
                    var windowMonitorLogCombinedFilePath = Path.Combine(logsPath, "window_monitor_logs.txt");
                    _logger.LogInformation("Moving window monitor log to logs folder: {WindowMonitorLogCombinedFilePath}", 
                        windowMonitorLogCombinedFilePath);
                    _fileSystemIOService.MoveFile(windowMonitorLogFilePath, windowMonitorLogCombinedFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving window monitor logs for date {Date}", date.ToString("yyyyMMdd"));
            }
        }

        private async Task MoveKeyboardLogs(DateTime date, string logsPath)
        {
            try
            {
                var keyboardLogFilePath = _keyboardLogIOService.GetFilePath(date);
                var keyboardLogExists = _fileSystemIOService.CheckIfFileExists(keyboardLogFilePath);
                
                if (keyboardLogExists)
                {
#if DEBUG
                    var keyboardLogCombinedFilePath = Path.Combine(logsPath, "keyboard_logs.txt");
                    _logger.LogInformation("Moving keyboard log to logs folder: {KeyboardLogCombinedFilePath}", 
                        keyboardLogCombinedFilePath);
                    _fileSystemIOService.MoveFile(keyboardLogFilePath, keyboardLogCombinedFilePath);
#else
                    _logger.LogInformation("Deleting keyboard log file: {KeyboardLogFilePath}", keyboardLogFilePath);
                    _fileSystemIOService.DeleteFile(keyboardLogFilePath);
#endif

                    // Remove from processing state using date key
                    var dateKey = ProcessingStateIOService.GetDateKey(date);
                    await _processingStateIOService.RemoveDate(dateKey);
                    _logger.LogDebug("Removed {DateKey} from processing state after aggregation", dateKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving keyboard logs for date {Date}", date.ToString("yyyyMMdd"));
            }
        }

        private async Task MoveMouseLogs(DateTime date, string logsPath)
        {
            try
            {
                var mouseLogFilePath = _mouseLogIOService.GetFilePath(date);
                var mouseLogExists = _fileSystemIOService.CheckIfFileExists(mouseLogFilePath);
                
                if (mouseLogExists)
                {
                    var mouseLogCombinedFilePath = Path.Combine(logsPath, "mouse_logs.txt");
                    _logger.LogInformation("Moving mouse log to logs folder: {MouseLogCombinedFilePath}", 
                        mouseLogCombinedFilePath);
                    _fileSystemIOService.MoveFile(mouseLogFilePath, mouseLogCombinedFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving mouse logs for date {Date}", date.ToString("yyyyMMdd"));
            }
        }

        private async Task MoveEmbeddings(DateTime date, string embeddingsPath)
        {
            try
            {
                var embeddingsDir = _embeddingIOService.GetFolderPath(date);
                var embeddingsDirExists = _fileSystemIOService.CheckIfDirectoryExists(embeddingsDir);
                
                if (embeddingsDirExists)
                {
                    var dateStr = date.ToString("yyyyMMdd");
                    var embeddingsCombinedDir = Path.Combine(embeddingsPath, dateStr);
                    _logger.LogInformation("Moving embeddings folder to embeddings path: {EmbeddingsCombinedDir}", 
                        embeddingsCombinedDir);
                    _fileSystemIOService.MoveFolder(embeddingsDir, embeddingsCombinedDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving embeddings for date {Date}", date.ToString("yyyyMMdd"));
            }
        }

        private async Task MoveApplicationLogs(DateTime date, string logsPath)
        {
            try
            {
                var dateStr = date.ToString("yyyyMMdd");
                
                // Generate log patterns dynamically from LaunchMode enum
                var launchModes = Enum.GetValues<LaunchMode>();
                var logPatterns = launchModes.Select(mode => 
                    $"application-{mode.ToString().ToLower()}-{dateStr}.log").ToArray();

                foreach (var pattern in logPatterns)
                {
                    var logFilePath = _fileSystemIOService.GetFullPath(pattern);
                    if (_fileSystemIOService.CheckIfFileExists(logFilePath))
                    {
                        var destinationPath = Path.Combine(logsPath, pattern);
                        _logger.LogInformation("Moving application log to logs folder: {LogFile} -> {Destination}", 
                            pattern, destinationPath);
                        _fileSystemIOService.MoveFile(logFilePath, destinationPath);
                    }
                    else
                    {
                        _logger.LogDebug("Application log file not found: {LogFile}", pattern);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving application logs for date {Date}", date.ToString("yyyyMMdd"));
            }
        }

    }
}