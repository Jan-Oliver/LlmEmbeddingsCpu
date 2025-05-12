using System.Timers;
using LlmEmbeddingsCpu.Common.Extensions;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using LlmEmbeddingsCpu.Data.FileStorage;
using LlmEmbeddingsCpu.Data.MouseInputStorage;


namespace LlmEmbeddingsCpu.Services.BackgroundProcessing
{
    public class ScheduledProcessingService: IDisposable
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly FileStorageService _fileStorageService;
        private readonly EmbeddingStorageService _embeddingStorageService;
        private readonly KeyboardInputStorageService _keyboardInputStorageService;
        private readonly MouseInputStorageService _mouseInputStorageService;
        private readonly System.Timers.Timer _timer;
        private TimeSpan _scheduleTime;
        private readonly string _processedMarkerFile = "processed_dates.txt";
        private readonly SemaphoreSlim _processingLock = new(1, 1);

        public ScheduledProcessingService(
            FileStorageService fileStorageService, 
            IEmbeddingService embeddingService,
            EmbeddingStorageService embeddingStorageService,
            KeyboardInputStorageService keyboardInputStorageService,
            MouseInputStorageService mouseInputStorageService)
        {
            _fileStorageService = fileStorageService;
            _embeddingService = embeddingService;
            _embeddingStorageService = embeddingStorageService;
            _keyboardInputStorageService = keyboardInputStorageService;
            _mouseInputStorageService = mouseInputStorageService;
            _timer = new System.Timers.Timer(60000); // Check every minute
            _timer.Elapsed += OnTimerElapsed;
        }

        public void ScheduleProcessingAsync(TimeSpan timeOfDay)
        {
            _scheduleTime = timeOfDay;
            _timer.Start();
            
            Console.WriteLine($"Scheduled processing for {_scheduleTime:hh\\:mm\\:ss} daily");
            
            // Calculate time until first run
            var now = DateTime.Now.TimeOfDay;
            var timeUntilFirstRun = _scheduleTime - now;
            if (timeUntilFirstRun < TimeSpan.Zero)
            {
                timeUntilFirstRun = TimeSpan.FromDays(1) + timeUntilFirstRun;
            }
            
            Console.WriteLine($"First run in {timeUntilFirstRun:hh\\:mm\\:ss}");
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
                
                Console.WriteLine("Starting text processing...");
                
                // Get all dates that need processing
                var datesToProcess = _keyboardInputStorageService.GetDatesToProcess();
                
                Console.WriteLine($"Found {datesToProcess.Count()} dates to process");
                
                foreach (var dateToProcess in datesToProcess)
                {
                    Console.WriteLine($"Processing date: {dateToProcess:yyyy-MM-dd}");
                    
                    // Get logs for this date
                    var logs = await _keyboardInputStorageService.GetPreviousLogsAsync(dateToProcess);
                    var keyboardLogs = logs.Select(log => log.Content).ToList();
                    
                    Console.WriteLine($"Processing {keyboardLogs.Count} keyboard logs");
                    
                    // Process the logs and generate embeddings
                    var embeddings = new List<Embedding>();
                    
                    foreach (var text in keyboardLogs)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            string decryptedText = text.FromRot13();
                            Console.WriteLine($"Processing text: {decryptedText}");
                            var embedding = await _embeddingService.GenerateEmbeddingAsync(decryptedText);
                            embeddings.Add(embedding);
                        }
                    }
                    
                    Console.WriteLine($"Generated {embeddings.Count} embeddings");
                    
                    // Save embeddings
                    await _embeddingStorageService.SaveEmbeddingsAsync(embeddings);
                    
                    // Archive logs for this date
                    ArchiveProcessedLogsAsync(dateToProcess);
                    
                    Console.WriteLine($"Completed processing for date {dateToProcess:yyyy-MM-dd}");
                }
                
                Console.WriteLine("Text processing completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during text processing: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
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
                Console.WriteLine($"Archived logs for {date:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error archiving logs for {date:yyyy-MM-dd}: {ex.Message}");
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