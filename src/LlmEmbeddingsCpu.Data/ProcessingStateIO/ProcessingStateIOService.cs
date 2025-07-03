using LlmEmbeddingsCpu.Data.FileSystemIO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LlmEmbeddingsCpu.Data.ProcessingStateIO
{
    /// <summary>
    /// Centralized service for managing processing state across all processing services.
    /// </summary>
    public class ProcessingStateIOService
    {
        private readonly ILogger<ProcessingStateIOService> _logger;
        private readonly FileSystemIOService _fileSystemIOService;
        private const string ProcessingStatePath = "processing_state.json";

        public ProcessingStateIOService(
            ILogger<ProcessingStateIOService> logger,
            FileSystemIOService fileSystemIOService)
        {
            _logger = logger;
            _fileSystemIOService = fileSystemIOService;
        }

        /// <summary>
        /// Loads the processing state from disk.
        /// </summary>
        /// <returns>Dictionary mapping date keys to processed line counts.</returns>
        public Dictionary<string, int> LoadProcessingState()
        {
            try
            {
                var json = _fileSystemIOService.ReadFileIfExists(ProcessingStatePath);
                
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("Processing state file not found, returning empty state");
                    return new Dictionary<string, int>();
                }

                var state = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                _logger.LogDebug("Loaded processing state with {Count} entries", state.Count);
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading processing state, returning empty state");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Saves the entire processing state to disk.
        /// </summary>
        /// <param name="processingState">The processing state to save.</param>
        public async Task SaveProcessingState(Dictionary<string, int> processingState)
        {
            try
            {
                var json = JsonConvert.SerializeObject(processingState, Formatting.Indented);
                await _fileSystemIOService.WriteFileAsync(ProcessingStatePath, json, false);
                
                _logger.LogDebug("Saved processing state with {Count} entries", processingState.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving processing state");
            }
        }

        /// <summary>
        /// Updates the processed count for a specific date.
        /// </summary>
        /// <param name="dateKey">The date key in yyyyMMdd format.</param>
        /// <param name="processedCount">The number of processed lines.</param>
        public async Task UpdateProcessedCount(string dateKey, int processedCount)
        {
            try
            {
                var processingState = LoadProcessingState();
                processingState[dateKey] = processedCount;
                await SaveProcessingState(processingState);
                
                _logger.LogDebug("Updated processing state for {DateKey}: {ProcessedCount} logs", 
                    dateKey, processedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating processing state for {DateKey}", dateKey);
            }
        }

        /// <summary>
        /// Gets the processed count for a specific date.
        /// </summary>
        /// <param name="dateKey">The date key in yyyyMMdd format.</param>
        /// <returns>The number of processed lines for the date, or 0 if not found.</returns>
        public int GetProcessedCount(string dateKey)
        {
            try
            {
                var processingState = LoadProcessingState();
                return processingState.GetValueOrDefault(dateKey, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting processed count for {DateKey}", dateKey);
                return 0;
            }
        }

        /// <summary>
        /// Removes a date from the processing state.
        /// </summary>
        /// <param name="dateKey">The date key in yyyyMMdd format.</param>
        public async Task RemoveDate(string dateKey)
        {
            try
            {
                var processingState = LoadProcessingState();
                if (processingState.Remove(dateKey))
                {
                    await SaveProcessingState(processingState);
                    _logger.LogDebug("Removed {DateKey} from processing state", dateKey);
                }
                else
                {
                    _logger.LogDebug("Date {DateKey} not found in processing state", dateKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing {DateKey} from processing state", dateKey);
            }
        }

        /// <summary>
        /// Removes multiple dates from the processing state.
        /// </summary>
        /// <param name="dateKeys">The date keys to remove.</param>
        public async Task RemoveDates(IEnumerable<string> dateKeys)
        {
            try
            {
                var processingState = LoadProcessingState();
                bool modified = false;
                
                foreach (var dateKey in dateKeys)
                {
                    if (processingState.Remove(dateKey))
                    {
                        modified = true;
                        _logger.LogDebug("Removed {DateKey} from processing state", dateKey);
                    }
                }
                
                if (modified)
                {
                    await SaveProcessingState(processingState);
                    _logger.LogDebug("Removed multiple dates from processing state");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple dates from processing state");
            }
        }

        /// <summary>
        /// Converts a DateTime to a date key string.
        /// </summary>
        /// <param name="date">The date to convert.</param>
        /// <returns>The date key in yyyyMMdd format.</returns>
        public static string GetDateKey(DateTime date)
        {
            return date.ToString("yyyyMMdd");
        }
    }
}