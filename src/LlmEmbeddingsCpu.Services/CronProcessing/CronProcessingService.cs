using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Data.EmbeddingIO;
using LlmEmbeddingsCpu.Data.KeyboardLogIO;
using LlmEmbeddingsCpu.Data.ProcessingStateIO;
using LlmEmbeddingsCpu.Common.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LlmEmbeddingsCpu.Services.CronProcessing
{
    /// <summary>
    /// Provides brute-force processing of all log files without resource checks.
    /// </summary>
    public class CronProcessingService(
        ILogger<CronProcessingService> logger,
        EmbeddingIOService embeddingIOService,
        KeyboardLogIOService keyboardLogIOService,
        IEmbeddingService embeddingService,
        ProcessingStateIOService processingStateIOService
    )
    {
        private readonly ILogger<CronProcessingService> _logger = logger;
        private readonly EmbeddingIOService _embeddingIOService = embeddingIOService;
        private readonly KeyboardLogIOService _keyboardLogIOService = keyboardLogIOService;
        private readonly IEmbeddingService _embeddingService = embeddingService;
        private readonly ProcessingStateIOService _processingStateIOService = processingStateIOService;

        private const int BatchSize = 10;

        /// <summary>
        /// Starts the cron processing to complete all unprocessed work.
        /// </summary>
        public async Task StartProcessingAsync()
        {
            _logger.LogInformation("Starting cron processing service");

            try
            {
                var datesToProcess = _keyboardLogIOService.GetDatesToProcess().ToList();

                _logger.LogInformation("Found {DateCount} dates to process", datesToProcess.Count);

                // Process all dates without resource checks
                foreach (var date in datesToProcess)
                {
                    await ProcessDateCompletely(date);
                }

                _logger.LogInformation("Cron processing completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cron processing");
            }
        }

        private async Task ProcessDateCompletely(DateTime date)
        {
            try
            {
                var dateKey = ProcessingStateIOService.GetDateKey(date);
                var allLogs = (await _keyboardLogIOService.GetPreviousLogsAsyncDecrypted(date)).ToList();
                var processedCount = _processingStateIOService.GetProcessedCount(dateKey);

                if (processedCount >= allLogs.Count)
                {
                    _logger.LogDebug("Date {Date} already fully processed ({ProcessedCount}/{TotalCount})", 
                        dateKey, processedCount, allLogs.Count);
                    return;
                }

                _logger.LogInformation("Processing date {Date}: {ProcessedCount}/{TotalCount} logs completed", 
                    dateKey, processedCount, allLogs.Count);

                // Process remaining logs in batches without resource checks
                while (processedCount < allLogs.Count)
                {
                    var batchLogs = allLogs.Skip(processedCount).Take(BatchSize).ToList();
                    
                    if (batchLogs.Count == 0)
                    {
                        _logger.LogWarning("No logs processed in batch for {Date}, breaking loop", dateKey);
                        break;
                    }

                    // Process batch and generate embeddings
                    await ProcessKeyboardLogBatch(batchLogs, date);
                    processedCount += batchLogs.Count;

                    // Update processing state
                    await _processingStateIOService.UpdateProcessedCount(dateKey, processedCount);

                    _logger.LogDebug("Processed batch for {Date}: {ProcessedCount}/{TotalCount}", 
                        dateKey, processedCount, allLogs.Count);
                }

                _logger.LogInformation("Completed processing date {Date}: {ProcessedCount}/{TotalCount}", 
                    dateKey, processedCount, allLogs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing date {Date}", date.ToString("yyyyMMdd"));
            }
        }


        private async Task ProcessKeyboardLogBatch(List<Core.Models.KeyboardInputLog> keyboardLogs, DateTime date)
        {
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