using System.Globalization;
using LlmEmbeddingsCpu.Common.Extensions;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileStorage;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Data.WindowMonitorStorage
{
    /// <summary>
    /// Manages the storage and retrieval of active window logs.
    /// </summary>
    public class WindowMonitorStorageService(FileStorageService fileStorageService,
        ILogger<WindowMonitorStorageService> logger)
    {
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly string _windowMonitorLogBaseFileName = "window_monitor_logs";
        private readonly ILogger<WindowMonitorStorageService> _logger = logger;

        /// <summary>
        /// Generates a file path for a window monitor log file based on the specified date.
        /// </summary>
        /// <param name="date">The date for the log file.</param>
        /// <returns>A string representing the file path.</returns>
        public string GetFilePath(DateTime date)
        {
            string timestamp = date.ToString("yyyy-MM-dd");
            return $"{_windowMonitorLogBaseFileName}-{timestamp}.txt";
        }

        /// <summary>
        /// Asynchronously saves an active window log to a file.
        /// </summary>
        /// <param name="log">The <see cref="ActiveWindowLog"/> to save.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SaveLogAsync(ActiveWindowLog log)
        {
            string fileName = GetFilePath(DateTime.Now);
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.WindowTitle.ToRot13()}|{log.WindowHandle}|{log.ProcessName.ToRot13()}";
            
            _logger.LogInformation("Logging to {FileName}: {FormattedLog}", fileName, formattedLog);
            
            await _fileStorageService.WriteFileAsync(fileName, formattedLog + Environment.NewLine, true);
        }

        /// <summary>
        /// Retrieves a collection of dates for which window monitor log files exist and are ready to be processed.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{DateTime}"/> of dates to process.</returns>
        public IEnumerable<DateTime> GetDatesToProcess()
        {
            var files = _fileStorageService.ListFiles("*.txt");
            var logFiles = files.Where(f =>  
                f.StartsWith(_windowMonitorLogBaseFileName))
                .OrderBy(f => f);

            if (!logFiles.Any())
            {
                return Enumerable.Empty<DateTime>();
            }

            // Get all unique dates from the filenames using proper date extraction
            var currentDate = DateTime.Now.Date;
            return logFiles
                .Select(f => {
                    // Extract the date portion using substring
                    int dateStart = f.IndexOf('-') + 1;
                    int dateEnd = f.LastIndexOf('.');
                    if (dateStart > 0 && dateEnd > dateStart)
                    {
                        var dateStr = f.Substring(dateStart, dateEnd - dateStart);
                        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime logDate))
                        {
                            return logDate;
                        }
                    }
                    return DateTime.MinValue;
                })
                .Where(d => d != DateTime.MinValue && d < currentDate)
                .Distinct()
                .OrderBy(d => d);
        }
    }
}