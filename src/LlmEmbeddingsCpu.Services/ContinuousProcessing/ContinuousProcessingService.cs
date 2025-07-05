using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Data.EmbeddingIO;
using LlmEmbeddingsCpu.Data.KeyboardLogIO;
using LlmEmbeddingsCpu.Data.ProcessingStateIO;
using LlmEmbeddingsCpu.Common.Extensions;
using Microsoft.Extensions.Logging;
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
        private readonly EmbeddingIOService _embeddingIOService;
        private readonly KeyboardLogIOService _keyboardLogIOService;
        private readonly IEmbeddingService _embeddingService;
        private readonly ProcessingStateIOService _processingStateIOService;
        private readonly PerformanceCounter _cpuCounter;

        private const int BatchSize = 10;
        private const float CpuThreshold = 80.0f; // 80%

        public ContinuousProcessingService(
            ILogger<ContinuousProcessingService> logger,
            EmbeddingIOService embeddingIOService,
            KeyboardLogIOService keyboardLogIOService,
            IEmbeddingService embeddingService,
            ProcessingStateIOService processingStateIOService)
        {
            _logger = logger;
            _embeddingIOService = embeddingIOService;
            _keyboardLogIOService = keyboardLogIOService;
            _embeddingService = embeddingService;
            _processingStateIOService = processingStateIOService;
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
                _logger.LogInformation("Processing date: {TargetDate}", targetDate.ToString("yyyyMMdd"));

                // Step 2: Load processing state for this date
                var dateKey = ProcessingStateIOService.GetDateKey(targetDate);
                var processedCount = _processingStateIOService.GetProcessedCount(dateKey);

                // Step 3: Get all keyboard logs for the target date
                var allLogs = (await _keyboardLogIOService.GetPreviousLogsAsyncDecrypted(targetDate)).ToList();
                
                if (allLogs.Count == 0)
                {
                    _logger.LogInformation("No keyboard logs found for date {Date}", targetDate.ToString("yyyyMMdd"));
                    return;
                }

                _logger.LogInformation("Found {TotalLogs} logs for {Date}, {ProcessedCount} already processed", 
                    allLogs.Count, targetDate.ToString("yyyyMMdd"), processedCount);

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
                        _logger.LogInformation("No more logs to process for date {Date}", targetDate.ToString("yyyyMMdd"));
                        break;
                    }

                    // Process batch and generate embeddings
                    await ProcessKeyboardLogBatch(batchLogs, targetDate);
                    processedCount += batchLogs.Count;

                    // Update processing state
                    await _processingStateIOService.UpdateProcessedCount(dateKey, processedCount);

                    _logger.LogDebug("Processed batch of {Count} logs, total processed: {Total}/{TotalLogs}", 
                        batchLogs.Count, processedCount, allLogs.Count);
                }

                _logger.LogInformation("Completed processing for date {Date}: {ProcessedCount}/{TotalLogs}", 
                    targetDate.ToString("yyyyMMdd"), processedCount, allLogs.Count);
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
                var availableDates = _keyboardLogIOService.GetDatesToProcess(includeToday: true).ToList();

                // Find dates that have unprocessed logs
                var unprocessedDates = new List<DateTime>();
                
                foreach (var date in availableDates)
                {
                    var dateKey = ProcessingStateIOService.GetDateKey(date);
                    var processedCount = _processingStateIOService.GetProcessedCount(dateKey);
                    if (await HasUnprocessedLogs(date, processedCount))
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
                var logs = await _keyboardLogIOService.GetPreviousLogsAsyncDecrypted(date);
                var totalCount = logs.Count();
                return totalCount > processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking unprocessed logs for date {Date}", date.ToString("yyyyMMdd"));
                return false;
            }
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

            // Check if any of the logs are empty
            var nonEmptyLogs = keyboardLogs.Where(log => !string.IsNullOrWhiteSpace(log.Content)).ToList();
            if (nonEmptyLogs.Count == 0)
            {
                _logger.LogWarning("Skipping batch due to empty logs");
                return;
            }
            
            try
                {
                // Generate embedding
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(nonEmptyLogs);
                
                // Store embedding using the date parameter
                await _embeddingIOService.SaveEmbeddingsAsync(embeddings, date);
                
                _logger.LogDebug("Generated and stored embedding for content of length {Length}", 
                    nonEmptyLogs.Sum(log => log.Content.Length));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing keyboard log: {LogId}, Content: {Content}", 
                    nonEmptyLogs.First().Timestamp, 
                    nonEmptyLogs.First().Content is { Length: > 50 } content ? content[..50] : nonEmptyLogs.First().Content);
            }
        
        }


    }
}