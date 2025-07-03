using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Data.FileStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using LlmEmbeddingsCpu.Data.MouseInputStorage;
using LlmEmbeddingsCpu.Data.WindowMonitorStorage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        FileStorageService fileStorageService,
        EmbeddingStorageService embeddingStorageService,
        KeyboardInputStorageService keyboardInputStorageService,
        MouseInputStorageService mouseInputStorageService,
        WindowMonitorStorageService windowMonitorStorageService
    )
    {
        private readonly ILogger<AggregationService> _logger = logger;
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly EmbeddingStorageService _embeddingStorageService = embeddingStorageService;
        private readonly KeyboardInputStorageService _keyboardInputStorageService = keyboardInputStorageService;
        private readonly MouseInputStorageService _mouseInputStorageService = mouseInputStorageService;
        private readonly WindowMonitorStorageService _windowMonitorStorageService = windowMonitorStorageService;

        private const string ProcessingStatePath = "processing_state.json";
        private const string UploadPath = "upload-queue";

        /// <summary>
        /// Starts the aggregation service to archive completed log files.
        /// </summary>
        public async Task StartAggregationAsync()
        {
            _logger.LogInformation("Starting aggregation service");

            try
            {
                var processingState = LoadProcessingState();
                var completedDates = await GetCompletedDatesAsync(processingState);

                _logger.LogInformation("Found {CompletedCount} completed dates to aggregate", completedDates.Count);

                foreach (var date in completedDates)
                {
                    await AggregateCompletedDate(date, processingState);
                }

                // Save updated processing state
                await SaveProcessingState(processingState);

                _logger.LogInformation("Aggregation service completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during aggregation");
            }
        }

        private async Task<List<DateTime>> GetCompletedDatesAsync(Dictionary<string, int> processingState)
        {
            var completedDates = new List<DateTime>();
            var today = DateTime.Today;

            try
            {
                // Get all available dates from past days only using the storage service
                var availableDates = _keyboardInputStorageService.GetDatesToProcess()
                    .Where(date => date.Date < today)
                    .ToList();

                foreach (var date in availableDates)
                {
                    var dateKey = GetDateKey(date);
                    if (await IsDateCompleteAsync(date, processingState.GetValueOrDefault(dateKey, 0)))
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

        private string GetDateKey(DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }

        private async Task<bool> IsDateCompleteAsync(DateTime date, int processedCount)
        {
            try
            {
                var logs = await _keyboardInputStorageService.GetPreviousLogsAsyncDecrypted(date);
                var totalCount = logs.Count();
                
                var isComplete = totalCount == processedCount;
                
                _logger.LogDebug("Date {Date}: {TotalCount} logs total, {ProcessedCount} processed, Complete: {IsComplete}", 
                    date.ToString("yyyy-MM-dd"), totalCount, processedCount, isComplete);
                
                return isComplete;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking completion status for date {Date}", date.ToString("yyyy-MM-dd"));
                return false;
            }
        }

        private async Task AggregateCompletedDate(DateTime date, Dictionary<string, int> processingState)
        {
            try
            {
                _logger.LogInformation("Aggregating completed date: {Date}", date.ToString("yyyy-MM-dd"));
                
                // Create combined directory
                var hostname = Environment.MachineName;
                var userId = Environment.UserName;
                var dateStr = date.ToString("yyyy-MM-dd");
                var combinedPath = Path.Combine(UploadPath, $"{hostname}-{userId}-{dateStr}");
                
                _logger.LogInformation("Combined path: {CombinedPath}", combinedPath);
                _fileStorageService.EnsureDirectoryExists(combinedPath);

                // Move window monitor logs
                await MoveWindowLogs(date, combinedPath);

                // Handle keyboard logs (delete in production, archive in debug)
                await HandleKeyboardLogs(date, processingState);

                // Move mouse logs
                await MoveMouse​Logs(date, combinedPath);

                // Move embeddings folder
                await MoveEmbeddings(date, combinedPath);

                _logger.LogInformation("Successfully aggregated date: {Date}", date.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating date {Date}", date.ToString("yyyy-MM-dd"));
            }
        }

        private async Task MoveWindowLogs(DateTime date, string combinedPath)
        {
            try
            {
                var windowMonitorLogFilePath = _windowMonitorStorageService.GetFilePath(date);
                var windowMonitorLogExists = _fileStorageService.CheckIfFileExists(windowMonitorLogFilePath);
                
                if (windowMonitorLogExists)
                {
                    var windowMonitorLogCombinedFilePath = Path.Combine(combinedPath, "window_monitor_logs.txt");
                    _logger.LogInformation("Moving window monitor log to combined path: {WindowMonitorLogCombinedFilePath}", 
                        windowMonitorLogCombinedFilePath);
                    _fileStorageService.MoveFile(windowMonitorLogFilePath, windowMonitorLogCombinedFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving window monitor logs for date {Date}", date.ToString("yyyy-MM-dd"));
            }
        }

        private async Task HandleKeyboardLogs(DateTime date, Dictionary<string, int> processingState)
        {
            try
            {
                var keyboardLogFilePath = _keyboardInputStorageService.GetFilePath(date);
                var keyboardLogExists = _fileStorageService.CheckIfFileExists(keyboardLogFilePath);
                
                if (keyboardLogExists)
                {
                    #if DEBUG
                    _logger.LogInformation("Moving keyboard log to deleted directory: {KeyboardLogFilePath}", keyboardLogFilePath);
                    _keyboardInputStorageService.MarkFileAsDeleted(date);
                    #else
                    _logger.LogInformation("Deleting keyboard log: {KeyboardLogFilePath}", keyboardLogFilePath);
                    _keyboardInputStorageService.DeleteFile(date);
                    #endif

                    // Remove from processing state using date key
                    var dateKey = GetDateKey(date);
                    processingState.Remove(dateKey);
                    _logger.LogDebug("Removed {DateKey} from processing state after aggregation", dateKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling keyboard logs for date {Date}", date.ToString("yyyy-MM-dd"));
            }
        }

        private async Task MoveMouse​Logs(DateTime date, string combinedPath)
        {
            try
            {
                var mouseLogFilePath = _mouseInputStorageService.GetFilePath(date);
                var mouseLogExists = _fileStorageService.CheckIfFileExists(mouseLogFilePath);
                
                if (mouseLogExists)
                {
                    var mouseLogCombinedFilePath = Path.Combine(combinedPath, "mouse_logs.txt");
                    _logger.LogInformation("Moving mouse log to combined path: {MouseLogCombinedFilePath}", 
                        mouseLogCombinedFilePath);
                    _fileStorageService.MoveFile(mouseLogFilePath, mouseLogCombinedFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving mouse logs for date {Date}", date.ToString("yyyy-MM-dd"));
            }
        }

        private async Task MoveEmbeddings(DateTime date, string combinedPath)
        {
            try
            {
                var embeddingsDir = _embeddingStorageService.GetFolderPath(date);
                var embeddingsDirExists = _fileStorageService.CheckIfDirectoryExists(embeddingsDir);
                
                if (embeddingsDirExists)
                {
                    var embeddingsCombinedDir = Path.Combine(combinedPath, "embeddings");
                    _logger.LogInformation("Moving embeddings folder to combined path: {EmbeddingsCombinedDir}", 
                        embeddingsCombinedDir);
                    _fileStorageService.MoveFolder(embeddingsDir, embeddingsCombinedDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving embeddings for date {Date}", date.ToString("yyyy-MM-dd"));
            }
        }

        private Dictionary<string, int> LoadProcessingState()
        {
            try
            {
                var json = _fileStorageService.ReadFileIfExists(ProcessingStatePath);
                
                if (string.IsNullOrEmpty(json))
                {
                    return new Dictionary<string, int>();
                }

                return JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading processing state, returning empty state");
                return new Dictionary<string, int>();
            }
        }

        private async Task SaveProcessingState(Dictionary<string, int> processingState)
        {
            try
            {
                var json = JsonConvert.SerializeObject(processingState, Formatting.Indented);
                await _fileStorageService.WriteFileAsync(ProcessingStatePath, json, false);
                
                _logger.LogInformation("Updated processing state with {Count} entries", processingState.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving processing state");
            }
        }

    }
}