using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Data.FileStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using LlmEmbeddingsCpu.Common.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LlmEmbeddingsCpu.Services.ContinuousProcessing
{
    /// <summary>
    /// Provides continuous processing of log files with resource-aware batching.
    /// </summary>
    public class ContinuousProcessingService
    {
        private readonly ILogger<ContinuousProcessingService> _logger;
        private readonly FileStorageService _fileStorageService;
        private readonly EmbeddingStorageService _embeddingStorageService;
        private readonly KeyboardInputStorageService _keyboardInputStorageService;
        private readonly IEmbeddingService _embeddingService;
        private readonly PerformanceCounter _cpuCounter;

        private const string ProcessingStatePath = "processing_state.json";
        private const int BatchSize = 10;
        private const float CpuThreshold = 80.0f; // 80%
        private const int CpuCheckIntervalMs = 1000; // 1 second

        public ContinuousProcessingService(
            ILogger<ContinuousProcessingService> logger,
            FileStorageService fileStorageService,
            EmbeddingStorageService embeddingStorageService,
            KeyboardInputStorageService keyboardInputStorageService,
            IEmbeddingService embeddingService)
        {
            _logger = logger;
            _fileStorageService = fileStorageService;
            _embeddingStorageService = embeddingStorageService;
            _keyboardInputStorageService = keyboardInputStorageService;
            _embeddingService = embeddingService;
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            
            // Initialize CPU counter
            _cpuCounter.NextValue();
        }

        /// <summary>
        /// Starts the continuous processing with graceful shutdown loop.
        /// </summary>
        public async Task StartProcessingAsync()
        {
            _logger.LogInformation("Starting continuous processing service");

            try
            {
                // Step 1: Get dates that need processing
                var datesToProcess = await GetDatesToProcessAsync();
                if (!datesToProcess.Any())
                {
                    _logger.LogInformation("No dates found that need processing");
                    return;
                }

                var targetDate = datesToProcess.First();
                _logger.LogInformation("Processing date: {TargetDate}", targetDate.ToString("yyyy-MM-dd"));

                // Step 2: Load processing state for this date
                var processingState = LoadProcessingState();
                var dateKey = GetDateKey(targetDate);
                var processedCount = processingState.GetValueOrDefault(dateKey, 0);

                // Step 3: Get all keyboard logs for the target date
                var allLogs = (await _keyboardInputStorageService.GetPreviousLogsAsyncDecrypted(targetDate)).ToList();
                
                if (allLogs.Count == 0)
                {
                    _logger.LogInformation("No keyboard logs found for date {Date}", targetDate.ToString("yyyy-MM-dd"));
                    return;
                }

                _logger.LogInformation("Found {TotalLogs} logs for {Date}, {ProcessedCount} already processed", 
                    allLogs.Count, targetDate.ToString("yyyy-MM-dd"), processedCount);

                // Step 4: Process remaining logs in batches
                while (processedCount < allLogs.Count)
                {
                    // Check resources at the top of the loop
                    if (!CheckResourcesAvailable())
                    {
                        _logger.LogInformation("Resources insufficient, breaking processing loop");
                        break;
                    }

                    // Process the next batch
                    var batchLogs = allLogs.Skip(processedCount).Take(BatchSize).ToList();
                    if (batchLogs.Count == 0)
                    {
                        _logger.LogInformation("No more logs to process for date {Date}", targetDate.ToString("yyyy-MM-dd"));
                        break;
                    }

                    // Process batch and generate embeddings
                    await ProcessKeyboardLogBatch(batchLogs, targetDate);
                    processedCount += batchLogs.Count;

                    // Update processing state
                    await UpdateProcessingState(dateKey, processedCount);

                    _logger.LogDebug("Processed batch of {Count} logs, total processed: {Total}/{TotalLogs}", 
                        batchLogs.Count, processedCount, allLogs.Count);
                }

                _logger.LogInformation("Completed processing for date {Date}: {ProcessedCount}/{TotalLogs}", 
                    targetDate.ToString("yyyy-MM-dd"), processedCount, allLogs.Count);
            }
            finally
            {
                _logger.LogInformation("Continuous processing service completed");
            }
        }

        private async Task<List<DateTime>> GetDatesToProcessAsync()
        {
            try
            {
                var processingState = LoadProcessingState();
                var availableDates = _keyboardInputStorageService.GetDatesToProcess().ToList();

                // Find dates that have unprocessed logs
                var unprocessedDates = new List<DateTime>();
                
                foreach (var date in availableDates)
                {
                    var dateKey = GetDateKey(date);
                    if (await HasUnprocessedLogs(date, processingState.GetValueOrDefault(dateKey, 0)))
                    {
                        unprocessedDates.Add(date);
                    }
                }

                return unprocessedDates.OrderBy(d => d).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding dates to process");
                return new List<DateTime>();
            }
        }

        private async Task<bool> HasUnprocessedLogs(DateTime date, int processedCount)
        {
            try
            {
                var logs = await _keyboardInputStorageService.GetPreviousLogsAsyncDecrypted(date);
                var totalCount = logs.Count();
                return totalCount > processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking unprocessed logs for date {Date}", date.ToString("yyyy-MM-dd"));
                return false;
            }
        }

        private string GetDateKey(DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }

        private bool CheckResourcesAvailable()
        {
            try
            {
                var currentCpuUsage = _cpuCounter.NextValue();
                _logger.LogDebug("Current CPU usage: {CpuUsage:F2}%", currentCpuUsage);
                
                return currentCpuUsage < CpuThreshold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system resources");
                return false;
            }
        }

        private async Task ProcessKeyboardLogBatch(List<Core.Models.KeyboardInputLog> keyboardLogs, DateTime date)
        {
            foreach (var log in keyboardLogs)
            {
                try
                {
                    // Skip if content is empty or contains only special characters
                    if (string.IsNullOrWhiteSpace(log.Content) || log.Content.All(c => !char.IsLetterOrDigit(c)))
                        continue;

                    // Skip if content is too short
                    if (log.Content.Length < 3)
                        continue;

                    // Skip special key combinations (only process text)
                    if (log.Type != Core.Enums.KeyboardInputType.Text)
                        continue;

                    // Generate embedding
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(log);
                    
                    // Store embedding using the date parameter
                    await _embeddingStorageService.SaveEmbeddingAsync(embedding, date);
                    
                    _logger.LogDebug("Generated and stored embedding for content of length {Length}", 
                        log.Content.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing keyboard log: {LogId}, Content: {Content}", 
                        log.Timestamp, log.Content?.Substring(0, Math.Min(50, log.Content?.Length ?? 0)));
                }
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

        private async Task UpdateProcessingState(string dateKey, int processedCount)
        {
            try
            {
                var processingState = LoadProcessingState();
                processingState[dateKey] = processedCount;

                var json = JsonConvert.SerializeObject(processingState, Formatting.Indented);
                await _fileStorageService.WriteFileAsync(ProcessingStatePath, json, false);
                
                _logger.LogDebug("Updated processing state for {DateKey}: {ProcessedCount} logs", 
                    dateKey, processedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating processing state for {DateKey}", dateKey);
            }
        }

    }
}