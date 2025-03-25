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
                await _processingLock.WaitAsync();
                
                Console.WriteLine("Starting text processing...");
                
                // Get all dates that need processing
                var datesToProcess = _inputLogRepository.GetDatesToProcess();
                
                Console.WriteLine($"Found {datesToProcess.Count()} dates to process");
                
                foreach (var dateToProcess in datesToProcess)
                {
                    Console.WriteLine($"Processing date: {dateToProcess:yyyy-MM-dd}");
                    
                    // Get logs for this date
                    var logs = await _inputLogRepository.GetPreviousLogsAsync(dateToProcess);
                    var keyboardLogs = logs.Where(log => log.Type == InputType.Keyboard)
                                         .Select(log => log.Content)
                                         .ToList();
                    
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
                    await _embeddingRepository.SaveEmbeddingsAsync(embeddings);
                    
                    // Archive logs for this date
                    await ArchiveProcessedLogsAsync(dateToProcess);
                    
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

        private async Task ArchiveProcessedLogsAsync(DateTime date)
        {
            try
            {
                _inputLogRepository.MarkFileAsDeleted(date);
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
        }
    }
}