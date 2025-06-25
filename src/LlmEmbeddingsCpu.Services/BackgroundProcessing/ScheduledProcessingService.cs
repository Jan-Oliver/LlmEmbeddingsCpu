using System.Timers;
using System.IO;
using System.IO.Compression;
using LlmEmbeddingsCpu.Common.Extensions;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using Microsoft.Extensions.Logging;
using LlmEmbeddingsCpu.Data.MouseInputStorage;
using LlmEmbeddingsCpu.Data.WindowMonitorStorage;
using LlmEmbeddingsCpu.Data.FileStorage;

namespace LlmEmbeddingsCpu.Services.BackgroundProcessing
{
    /// <summary>
    /// Manages a scheduled background service to process and archive user activity logs.
    /// </summary>
    public class ScheduledProcessingService: IDisposable
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly EmbeddingStorageService _embeddingStorageService;
        private readonly KeyboardInputStorageService _keyboardInputStorageService;
        private readonly MouseInputStorageService _mouseInputStorageService;
        private readonly WindowMonitorStorageService _windowMonitorStorageService;

        private readonly FileStorageService _fileStorageService;
        private readonly System.Timers.Timer _timer;
        private TimeSpan _scheduleTime;
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        private readonly ILogger<ScheduledProcessingService> _logger;

        private readonly string _uploadPath = "upload-queue";

        private readonly string _archivePath = "archives";

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledProcessingService"/> class.
        /// </summary>
        public ScheduledProcessingService(
            ILogger<ScheduledProcessingService> logger,
            IEmbeddingService embeddingService,
            EmbeddingStorageService embeddingStorageService,
            KeyboardInputStorageService keyboardInputStorageService,
            MouseInputStorageService mouseInputStorageService,
            WindowMonitorStorageService windowMonitorStorageService,
            FileStorageService fileStorageService)
        {
            _logger = logger;
            _embeddingService = embeddingService;
            _embeddingStorageService = embeddingStorageService;
            _keyboardInputStorageService = keyboardInputStorageService;
            _mouseInputStorageService = mouseInputStorageService;
            _windowMonitorStorageService = windowMonitorStorageService;
            _fileStorageService = fileStorageService;
            _timer = new System.Timers.Timer(60000); // Check every minute
            _timer.Elapsed += OnTimerElapsed;
        }

        /// <summary>
        /// Schedules the daily processing task at a specified time.
        /// </summary>
        /// <param name="timeOfDay">The time of day to run the processing task.</param>
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

        /// <summary>
        /// Called when the timer elapses to check if processing should be initiated.
        /// </summary>
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

        /// <summary>
        /// Initiates the processing of logs and generation of embeddings immediately.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ProcessNowAsync()
        {
            try
            {
                await _processingLock.WaitAsync();
                
                _logger.LogInformation("Starting text processing...");
                
                // Get all dates that need processing
                var datesToProcessKeyboard = _keyboardInputStorageService.GetDatesToProcess();
                var datesToProcessMouse = _mouseInputStorageService.GetDatesToProcess();
                var datesToProcessWindowMonitor = _windowMonitorStorageService.GetDatesToProcess();

                // Fuse the dates to have all unique dates
                // Logic or, not union
                var datesToProcess = datesToProcessKeyboard
                    .Concat(datesToProcessMouse)
                    .Concat(datesToProcessWindowMonitor)
                    .Distinct()
                    .ToList();


                _logger.LogInformation("Found {DateCount} dates to process", datesToProcess.Count);
                _logger.LogInformation("Dates to process: {Dates}", string.Join(", ", datesToProcess));
                
                foreach (var dateToProcess in datesToProcessKeyboard)
                {
                    _logger.LogInformation("Processing keyboard inputs for date: {Date}", dateToProcess);
                    
                    // Check if there are logs for this date
                    // Get logs for this date
                    var logs = await _keyboardInputStorageService.GetPreviousLogsAsyncDecrypted(dateToProcess);
                    var keyboardLogs = logs.ToList();
                    
                    _logger.LogInformation("Processing {KeyboardLogCount} keyboard logs", keyboardLogs.Count);
                    
                    // Process the logs and generate embeddings
                    var embeddings = new List<Embedding>();
                    
                    foreach (var keyboardLog in keyboardLogs)
                    {
                        if (!string.IsNullOrWhiteSpace(keyboardLog.Content))
                        {
                            _logger.LogInformation("Processing text: {Text}", keyboardLog.Content);
                            var embedding = await _embeddingService.GenerateEmbeddingAsync(keyboardLog);
                            _logger.LogInformation("First 5 elements of embedding: {Embedding}", string.Join(", ", embedding.Vector.Take(5)));
                            embeddings.Add(embedding);
                        }
                    }
                    
                    _logger.LogInformation("Generated {EmbeddingCount} embeddings", embeddings.Count);
                    
                    // Save embeddings
                    await _embeddingStorageService.SaveEmbeddingsAsync(embeddings, dateToProcess);
                    
                    _logger.LogInformation("Completed processing for date {Date}", dateToProcess);
                }

                foreach (var dateToProcess in datesToProcess)
                {
                    _logger.LogInformation("Creating archive for date: {Date}", dateToProcess);
                    
                    UploadData(dateToProcess);
                }
                
                _logger.LogInformation("Text processing completed successfully");
                Console.WriteLine("Text processing completed successfully");
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

        /// <summary>
        /// Prepares the data for a given date for upload by moving it to a designated upload directory.
        /// </summary>
        /// <param name="date">The date of the data to be prepared for upload.</param>
        private void UploadData(DateTime date)
        {
            try
            {
                Console.WriteLine("Creating daily archive for date: " + date);
                var hostname = Environment.MachineName;
                var userId = Environment.UserName;
                var dateStr = date.ToString("yyyy-MM-dd");

                var uploadPath = Path.Combine(_uploadPath, $"{hostname}-{userId}-{dateStr}");
                _logger.LogInformation("Upload path: {UploadPath}", uploadPath);
                _fileStorageService.EnsureDirectoryExists(uploadPath);

                // Move the window monitor logs to the deleted directory
                var windowMonitorLogFilePath = _windowMonitorStorageService.GetFilePath(date);
                var windowMonitorLogExists = _fileStorageService.CheckIfFileExists(windowMonitorLogFilePath);
                var windowMonitorLogUploadFilePath = Path.Combine(uploadPath, "window_monitor_logs.txt");
                if (windowMonitorLogExists) {
                    _logger.LogInformation("Moving window monitor log to upload path: {WindowMonitorLogUploadFilePath}", windowMonitorLogUploadFilePath);
                    _fileStorageService.MoveFile(windowMonitorLogFilePath, windowMonitorLogUploadFilePath);
                }

                // Delete the keyboard logs
                var keyboardLogFilePath = _keyboardInputStorageService.GetFilePath(date);
                var keyboardLogExists = _fileStorageService.CheckIfFileExists(keyboardLogFilePath);
                if (keyboardLogExists) {
                    _logger.LogInformation("Moving keyboard log to upload path: {KeyboardLogUploadFilePath}", keyboardLogFilePath);
                    _keyboardInputStorageService.MarkFileAsDeleted(date);
                }
                
                // Move the mouse logs to the upload directory
                var mouseLogFilePath = _mouseInputStorageService.GetFilePath(date);
                var mouseLogExists = _fileStorageService.CheckIfFileExists(mouseLogFilePath);
                var mouseLogUploadFilePath = Path.Combine(uploadPath, "mouse_logs.txt");
                if (mouseLogExists) {
                    _logger.LogInformation("Moving mouse log to upload path: {MouseLogUploadFilePath}", mouseLogUploadFilePath);
                    _fileStorageService.MoveFile(mouseLogFilePath, mouseLogUploadFilePath);
                }

                // Move the embeddings folder to the upload directory
                var embeddingsDir = _embeddingStorageService.GetFolderPath(date);
                var embeddingsDirExists = _fileStorageService.CheckIfDirectoryExists(embeddingsDir);
                var embeddingsUploadDir = Path.Combine(uploadPath, "embeddings");
                if (embeddingsDirExists) {
                    _logger.LogInformation("Moving embeddings folder to upload path: {EmbeddingsUploadDir}", embeddingsUploadDir);
                    _fileStorageService.MoveFolder(embeddingsDir, embeddingsUploadDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create ZIP archive: {ErrorMessage}", ex.Message);
            }
        }

        /// <summary>
        /// Stops the scheduled processing task.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task StopScheduledProcessingAsync()
        {
            _timer.Stop();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Releases the resources used by the service.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
            _processingLock?.Dispose();
            // Prevents other classes from calling Dispose() on this instance
            GC.SuppressFinalize(this);
        }
    }
}