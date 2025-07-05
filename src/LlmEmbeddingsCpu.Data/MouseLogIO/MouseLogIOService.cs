using System.Globalization;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileSystemIO;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Data.MouseLogIO
{
    /// <summary>
    /// Manages the I/O operations for mouse input logs.
    /// </summary>
    public class MouseLogIOService(
        FileSystemIOService fileSystemIOService,
        ILogger<MouseLogIOService> logger)
    {
        private readonly FileSystemIOService _fileSystemIOService = fileSystemIOService;
        private readonly string _mouseLogBaseFileName = "mouse_logs";
        private readonly ILogger<MouseLogIOService> _logger = logger;

        /// <summary>
        /// Generates a file path for a mouse log file based on the specified date.
        /// </summary>
        /// <param name="date">The date for the log file.</param>
        /// <returns>A string representing the file path.</returns>
        public string GetFilePath(DateTime date)
        {
            string timestamp = date.ToString("yyyyMMdd");
            return $"{_mouseLogBaseFileName}-{timestamp}.txt";
        }

        /// <summary>
        /// Asynchronously saves a mouse input log to a file.
        /// </summary>
        /// <param name="log">The <see cref="MouseInputLog"/> to save.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SaveLogAsync(MouseInputLog log)
        {
            string fileName = GetFilePath(DateTime.Now);
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.Content.X}|{log.Content.Y}|{(int)log.Content.Button}|{log.Content.Clicks}|{log.Content.Delta}";
            
            _logger.LogDebug("Logging to {FileName}: {FormattedLog}", fileName, formattedLog);
            
            await _fileSystemIOService.WriteFileAsync(fileName, formattedLog + Environment.NewLine, true);
        }

        /// <summary>
        /// Retrieves a collection of dates for which mouse log files exist and are ready to be processed.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{DateTime}"/> of dates to process.</returns>
        public IEnumerable<DateTime> GetDatesToProcess()
        {
            var files = _fileSystemIOService.ListFiles("*.txt");
            var logFiles = files.Where(f =>  
                f.StartsWith(_mouseLogBaseFileName))
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
                        if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime logDate))
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