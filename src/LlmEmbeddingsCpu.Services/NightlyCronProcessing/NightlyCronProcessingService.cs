using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Data.FileStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using LlmEmbeddingsCpu.Common.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LlmEmbeddingsCpu.Services.NightlyCronProcessing
{
    /// <summary>
    /// Provides brute-force processing of all log files without resource checks.
    /// </summary>
    public class NightlyCronProcessingService(
        ILogger<NightlyCronProcessingService> logger,
        FileStorageService fileStorageService,
        EmbeddingStorageService embeddingStorageService,
        KeyboardInputStorageService keyboardInputStorageService,
        IEmbeddingService embeddingService
    )
    {
        private readonly ILogger<NightlyCronProcessingService> _logger = logger;
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly EmbeddingStorageService _embeddingStorageService = embeddingStorageService;
        private readonly KeyboardInputStorageService _keyboardInputStorageService = keyboardInputStorageService;
        private readonly IEmbeddingService _embeddingService = embeddingService;

        private const string ProcessingStatePath = "processing_state.json";
        private const int BatchSize = 10;

        /// <summary>
        /// Starts the nightly cron processing to complete all unprocessed work.
        /// </summary>
        public async Task StartProcessingAsync()
        {
            _logger.LogInformation("Starting nightly cron processing service");

            try
            {
                var processingState = LoadProcessingState();
                var datesToProcess = _keyboardInputStorageService.GetDatesToProcess().ToList();

                _logger.LogInformation("Found {DateCount} dates to process", datesToProcess.Count);

                // Process all dates without resource checks
                foreach (var date in datesToProcess)
                {
                    await ProcessDateCompletely(date, processingState);
                }

                _logger.LogInformation("Nightly cron processing completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during nightly cron processing");
            }
        }

        private async Task ProcessDateCompletely(DateTime date, Dictionary<string, int> processingState)
        {
            try
            {
                var dateKey = GetDateKey(date);
                var allLogs = (await _keyboardInputStorageService.GetPreviousLogsAsyncDecrypted(date)).ToList();
                var processedCount = processingState.GetValueOrDefault(dateKey, 0);

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
                    await UpdateProcessingState(dateKey, processedCount);

                    _logger.LogDebug("Processed batch for {Date}: {ProcessedCount}/{TotalCount}", 
                        dateKey, processedCount, allLogs.Count);
                }

                _logger.LogInformation("Completed processing date {Date}: {ProcessedCount}/{TotalCount}", 
                    dateKey, processedCount, allLogs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing date {Date}", date.ToString("yyyy-MM-dd"));
            }
        }

        private string GetDateKey(DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
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