using System.Globalization;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileStorage;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Data.WindowMonitorStorage
{
    public class WindowMonitorStorageService(FileStorageService fileStorageService,
        ILogger<WindowMonitorStorageService> logger)
    {
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly string _windowMonitorLogBaseFileName = "window_monitor_logs";
        private readonly ILogger<WindowMonitorStorageService> _logger = logger;

        public string GetFilePath(DateTime date)
        {
            string timestamp = date.ToString("yyyy-MM-dd");
            return $"{_windowMonitorLogBaseFileName}-{timestamp}.txt";
        }

        public async Task SaveLogAsync(ActiveWindowLog log)
        {
            string fileName = GetFilePath(DateTime.Now);
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.WindowTitle}|{log.WindowHandle}|{log.ProcessName}";
            
            _logger.LogInformation("Logging to {FileName}: {FormattedLog}", fileName, formattedLog);
            
            await _fileStorageService.WriteFileAsync(fileName, formattedLog + Environment.NewLine, true);
        }

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

        public async Task<IEnumerable<ActiveWindowLog>> GetPreviousLogAsync(DateTime date)
        {
            try
            {
                string windowMonitorFileName = GetFilePath(date);

                var logs = new List<ActiveWindowLog>();
                
               
                _logger.LogInformation("Reading mouse logs for: {Date}", date);
                string content = await _fileStorageService.ReadFileAsyncIfExists(windowMonitorFileName);
                if (string.IsNullOrEmpty(content))
                {
                    _logger.LogInformation("No mouse logs found for: {Date}", date);
                    return logs;
                }

                var windowMonitorLogs = ParseLogsFromContent(content, date);
                logs.AddRange(windowMonitorLogs);            
                
                return logs;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                _logger.LogError("Error retrieving logs: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Error retrieving logs: {ex.Message}", ex);
            }
        }

        private IEnumerable<ActiveWindowLog> ParseLogsFromContent(string content, DateTime fileDate)
        {
            if (string.IsNullOrEmpty(content))
                yield break;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ActiveWindowLog? log = null;
                try
                {
                    // Expected line format: [HH:mm:ss] X|Y|Button|Clicks|Delta
                    const string timestampFormat = "HH:mm:ss";
                    const int timestampLength = 8; // "HH:mm:ss".Length
                    const int expectedParts = 3; // WindowTitle, WindowHandle, ProcessName

                    // Basic validation for line structure
                    if (line.Length < timestampLength || line[0] != '[' || line[timestampLength + 1] != ']')
                    {
                        _logger.LogWarning("Skipping malformed log line (structure): {Line}", line);
                        continue;
                    }

                    string timestampStr = line.Substring(1, timestampLength); // Extract "HH:mm:ss"
                    string logContent = line.Substring(timestampLength + 2).Trim(); // Extract content after "] "

                    // Parse time part from the log line
                    if (DateTime.TryParseExact(timestampStr, timestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
                    {
                        // Combine the date from the filename (passed as fileDate) with the time from the log line
                        DateTime fullTimestamp = fileDate.Date.Add(timestamp.TimeOfDay);

                        // Parse content parts (WindowTitle, WindowHandle, ProcessName)
                        var parts = logContent.Split('|');
                        if (parts.Length == expectedParts)
                        {
                            if (nint.TryParse(parts[1], out nint handleValue))
                            {
                                string processName = parts[2];
                                string windowTitle = parts[0];

                                log = new ActiveWindowLog
                                {
                                    Timestamp = fullTimestamp,
                                    WindowTitle = windowTitle,
                                    WindowHandle = handleValue,
                                    ProcessName = processName
                                };
                            }
                            else
                            {
                                _logger.LogWarning("Skipping log line with invalid window handle '{WindowHandle}': {Line}", parts[0], line);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Skipping log line with incorrect number of content parts ({ContentParts} instead of {ExpectedParts}) '{LogContent}': {Line}", parts.Length, expectedParts, logContent, line);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Skipping log line with invalid timestamp format '{TimestampStr}': {Line}", timestampStr, line);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error parsing log line '{Line}': {ErrorMessage}", line, ex.Message);
                }

                if (log != null)
                    yield return log;
            }
        }
    }
}