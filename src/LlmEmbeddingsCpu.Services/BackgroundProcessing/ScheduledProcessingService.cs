using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using LlmEmbeddingsCpu.Common.Extensions;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Core.Enums;


namespace LlmEmbeddingsCpu.Services.BackgroundProcessing
{
    public class ScheduledProcessingService : IScheduledProcessingService, IDisposable
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IEmbeddingRepository _embeddingRepository;
        private readonly IInputLogRepository _inputLogRepository;
        private System.Timers.Timer _timer;
        private TimeSpan _scheduleTime;
        private readonly string _processedMarkerFile = "processed_dates.txt";
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);

        public ScheduledProcessingService(
            IFileStorageService fileStorageService, 
            IEmbeddingService embeddingService,
            IEmbeddingRepository embeddingRepository,
            IInputLogRepository inputLogRepository)
        {
            _fileStorageService = fileStorageService;
            _embeddingService = embeddingService;
            _embeddingRepository = embeddingRepository;
            _inputLogRepository = inputLogRepository;
            _timer = new System.Timers.Timer(60000); // Check every minute
            _timer.Elapsed += OnTimerElapsed;
        }

        public async Task ScheduleProcessingAsync(TimeSpan timeOfDay)
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

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
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
                // Use a semaphore to prevent concurrent processing
                await _processingLock.WaitAsync();
                
                Console.WriteLine("Starting text processing...");
                
                // Get yesterday's date (we process the previous day's logs)
                var dateToProcess = DateTime.Now.AddDays(-1).Date;
                
                // Check if we've already processed this date
                if (await HasBeenProcessedAsync(dateToProcess))
                {
                    Console.WriteLine($"Date {dateToProcess:yyyy-MM-dd} has already been processed");
                    return;
                }
                
                // Get the keyboard logs (the main text source)
                var keyboardLogs = await ReadKeyboardLogsAsync();
                
                if (keyboardLogs.Count == 0)
                {
                    Console.WriteLine("No keyboard logs found to process");
                    return;
                }
                
                Console.WriteLine($"Processing {keyboardLogs.Count} keyboard logs");
                
                // Process the logs and generate embeddings
                var embeddings = new List<Embedding>();
                
                foreach (var text in keyboardLogs)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Decrypt the ROT13 encoded text
                        string decryptedText = text.FromRot13();

                        // Log for debugging
                        Console.WriteLine($"Processing text: {decryptedText}");
                        
                        // Generate embedding
                        var embedding = await _embeddingService.GenerateEmbeddingAsync(decryptedText);
                        embeddings.Add(embedding);
                    }
                }
                
                Console.WriteLine($"Generated {embeddings.Count} embeddings");
                
                // Save embeddings
                await _embeddingRepository.SaveEmbeddingsAsync(embeddings);
                
                // Mark date as processed
                await MarkAsProcessedAsync(dateToProcess);
                
                // Archive or rotate logs to prevent reprocessing
                //await ArchiveProcessedLogsAsync();
                Console.WriteLine("Archiving logs in the future...");
                
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

        private async Task<bool> HasBeenProcessedAsync(DateTime date)
        {
            try
            {
                string content = await _fileStorageService.ReadTextAsync(_processedMarkerFile);
                string[] processedDates = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                return processedDates.Contains(date.ToString("yyyy-MM-dd"));
            }
            catch
            {
                // File might not exist yet
                return false;
            }
        }

        private async Task MarkAsProcessedAsync(DateTime date)
        {
            string dateString = date.ToString("yyyy-MM-dd");
            await _fileStorageService.WriteTextAsync(_processedMarkerFile, dateString + Environment.NewLine, true);
        }

        private async Task<List<string>> ReadKeyboardLogsAsync()
        {
            try
            {
                // Get yesterday's logs
                var yesterday = DateTime.Now.AddDays(-1).Date;
                var logs = await _inputLogRepository.GetPreviousLogsAsync(yesterday);
                
                return logs.Where(log => log.Type == InputType.Keyboard).Select(log => log.Content).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading keyboard logs: {ex.Message}");
                return new List<string>();
            }
        }

        private async Task ArchiveProcessedLogsAsync()
        {
            try
            {
                var yesterday = DateTime.Now.AddDays(-1).Date;
                _inputLogRepository.MarkFileAsDeleted(yesterday);
                Console.WriteLine($"Marked logs for {yesterday:yyyy-MM-dd} as processed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error archiving logs: {ex.Message}");
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
        }
    }
}