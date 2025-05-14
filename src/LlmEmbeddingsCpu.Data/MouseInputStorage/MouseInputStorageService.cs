using System.Globalization;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileStorage;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Data.MouseInputStorage
{
    public class MouseInputStorageService(
        FileStorageService fileStorageService,
        ILogger<MouseInputStorageService> logger)
    {
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly string _mouseLogBaseFileName = "mouse_logs";
        private readonly ILogger<MouseInputStorageService> _logger = logger;

        public string GetFilePath(DateTime date)
        {
            string timestamp = date.ToString("yyyy-MM-dd");
            return $"{_mouseLogBaseFileName}-{timestamp}.txt";
        }

        public async Task SaveLogAsync(MouseInputLog log)
        {
            string fileName = GetFilePath(DateTime.Now);
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.Content.X}|{log.Content.Y}|{(int)log.Content.Button}|{log.Content.Clicks}|{log.Content.Delta}";
            
            _logger.LogInformation("Logging to {FileName}: {FormattedLog}", fileName, formattedLog);
            
            await _fileStorageService.WriteFileAsync(fileName, formattedLog + Environment.NewLine, true);
        }

        public IEnumerable<DateTime> GetDatesToProcess()
        {
            var files = _fileStorageService.ListFiles("*.txt");
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

        public async Task<IEnumerable<MouseInputLog>> GetPreviousLogAsync(DateTime date)
        {
            try
            {
                string mouseInputFileName = GetFilePath(date);

                var logs = new List<MouseInputLog>();
                
               
                _logger.LogInformation("Reading mouse logs for: {Date}", date);
                string content = await _fileStorageService.ReadFileAsyncIfExists(mouseInputFileName);
                if (string.IsNullOrEmpty(content))
                {
                    _logger.LogInformation("No mouse logs found for: {Date}", date);
                    return logs;
                }

                var mouseLogs = ParseLogsFromContent(content, date);
                logs.AddRange(mouseLogs);            
                
                return logs;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                _logger.LogError("Error retrieving logs: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Error retrieving logs: {ex.Message}", ex);
            }
        }

        private IEnumerable<MouseInputLog> ParseLogsFromContent(string content, DateTime fileDate)
        {
            if (string.IsNullOrEmpty(content))
                yield break;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                MouseInputLog? log = null;
                try
                {
                    // Expected line format: [HH:mm:ss] X|Y|Button|Clicks|Delta
                    const string timestampFormat = "HH:mm:ss";
                    const int timestampLength = 8; // "HH:mm:ss".Length
                    const int expectedParts = 5; // X, Y, Button, Clicks, Delta

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

                        // Parse content parts (X, Y, Button, Clicks, Delta)
                        var parts = logContent.Split('|');
                        if (parts.Length == expectedParts)
                        {
                            // Attempt to parse each part as an integer
                            if (int.TryParse(parts[0], out int x) &&
                                int.TryParse(parts[1], out int y) &&
                                int.TryParse(parts[2], out int buttonInt) &&
                                int.TryParse(parts[3], out int clicks) &&
                                int.TryParse(parts[4], out int delta))
                            {
                                // Validate and convert integer button value back to MouseButtons enum
                                if (Enum.IsDefined(typeof(MouseButtons), buttonInt))
                                {
                                    MouseButtons button = (MouseButtons)buttonInt;
                                    MouseEventArgs mouseArgs = new MouseEventArgs(button, clicks, x, y, delta);
                                    log = new MouseInputLog
                                    {
                                        Timestamp = fullTimestamp,
                                        Content = mouseArgs
                                    };
                                }
                                else
                                {
                                    _logger.LogWarning("Skipping log line with invalid Button value '{ButtonValue}': {Line}", parts[2], line);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Skipping log line with invalid integer format in content '{LogContent}': {Line}", logContent, line);
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