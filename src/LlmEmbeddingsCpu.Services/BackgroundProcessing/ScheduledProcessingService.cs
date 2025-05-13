using System.Timers;
using LlmEmbeddingsCpu.Common.Extensions;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Services.BackgroundProcessing
{
    public class ScheduledProcessingService: IDisposable
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly EmbeddingStorageService _embeddingStorageService;
        private readonly KeyboardInputStorageService _keyboardInputStorageService;
        private readonly System.Timers.Timer _timer;
        private TimeSpan _scheduleTime;
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        private readonly ILogger<ScheduledProcessingService> _logger;

        public ScheduledProcessingService(
            ILogger<ScheduledProcessingService> logger,
            IEmbeddingService embeddingService,
            EmbeddingStorageService embeddingStorageService,
            KeyboardInputStorageService keyboardInputStorageService)
        {
            _logger = logger;
            _embeddingService = embeddingService;
            _embeddingStorageService = embeddingStorageService;
            _keyboardInputStorageService = keyboardInputStorageService;
            _timer = new System.Timers.Timer(60000); // Check every minute
            _timer.Elapsed += OnTimerElapsed;
        }

        public void ScheduleProcessingAsync(TimeSpan timeOfDay)
        {
            _scheduleTime = timeOfDay;
            _timer.Start();
            
            _logger.LogInformation("Scheduled processing for {ScheduleTime:hh\\:mm\\:ss} daily", _scheduleTime);
            
            // Calculate time until first run
            var now = DateTime.Now.TimeOfDay;
            var timeUntilFirstRun = _scheduleTime - now;
            if (timeUntilFirstRun < TimeSpan.Zero)
            {
                timeUntilFirstRun = TimeSpan.FromDays(1) + timeUntilFirstRun;
            }
            
            _logger.LogInformation("First run in {TimeUntilFirstRun:hh\\:mm\\:ss}", timeUntilFirstRun);
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            // Check if it's time to process
            var now = DateTime.Now.TimeOfDay;
            var timeDifference = Math.Abs((now - _scheduleTime).TotalMinutes);
            
            if (timeDifference < 1) // Within 1 minute of scheduled time
            {
                // Run processing asynchronously
                _ = ProcessNowAsync();
            }
        }

        public async Task ProcessNowAsync()
        {
            try
            {
                await _processingLock.WaitAsync();
                
                _logger.LogInformation("Starting text processing...");
                
                // Get all dates that need processing
                var datesToProcess = _keyboardInputStorageService.GetDatesToProcess();
                
                _logger.LogInformation("Found {DateCount} dates to process", datesToProcess.Count());
                
                foreach (var dateToProcess in datesToProcess)
                {
                    _logger.LogInformation("Processing date: {Date}", dateToProcess);
                    
                    // Get logs for this date
                    var logs = await _keyboardInputStorageService.GetPreviousLogsAsync(dateToProcess);
                    var keyboardLogs = logs.Select(log => log.Content).ToList();
                    
                    _logger.LogInformation("Processing {KeyboardLogCount} keyboard logs", keyboardLogs.Count);
                    
                    // Process the logs and generate embeddings
                    var embeddings = new List<Embedding>();
                    
                    foreach (var text in keyboardLogs)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            string decryptedText = text.FromRot13();
                            _logger.LogInformation("Processing text: {Text}", decryptedText);
                            var embedding = await _embeddingService.GenerateEmbeddingAsync(decryptedText);
                            _logger.LogInformation("First 5 elements of embedding: {Embedding}", string.Join(", ", embedding.Vector.Take(5)));
                            embeddings.Add(embedding);
                        }
                    }
                    
                    _logger.LogInformation("Generated {EmbeddingCount} embeddings", embeddings.Count);
                    
                    // Save embeddings
                    await _embeddingStorageService.SaveEmbeddingsAsync(embeddings, dateToProcess.ToString("yyyy-MM-dd"));
                    
                    // Archive logs for this date
                    ArchiveProcessedLogsAsync(dateToProcess);
                    
                    _logger.LogInformation("Completed processing for date {Date}", dateToProcess);
                }
                
                _logger.LogInformation("Text processing completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during text processing: {ErrorMessage}", ex.Message);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private void ArchiveProcessedLogsAsync(DateTime date)
        {
            try
            {
                _keyboardInputStorageService.MarkFileAsDeleted(date);
                _logger.LogInformation("Archived logs for {Date}", date);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error archiving logs for {Date}: {ErrorMessage}", date, ex.Message);
            }
        }

        public async Task StopScheduledProcessingAsync()
        {
            _timer.Stop();
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _processingLock?.Dispose();
            // Prevents other classes from calling Dispose() on this instance
            GC.SuppressFinalize(this);
        }
    }
}