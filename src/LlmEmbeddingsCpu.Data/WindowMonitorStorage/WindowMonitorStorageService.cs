using System.Globalization;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileStorage;

namespace LlmEmbeddingsCpu.Data.WindowMonitorStorage
{
    public class WindowMonitorStorageService(FileStorageService fileStorageService)
    {
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly string _windowMonitorLogBaseFileName = "window_monitor_logs";

        private string GetCurrentFileName()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd");
            return $"{_windowMonitorLogBaseFileName}-{timestamp}.txt";
        }

        public async Task SaveLogAsync(ActiveWindowLog log)
        {
            string fileName = GetCurrentFileName();
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.WindowTitle}|{log.WindowHandle}|{log.ProcessName}";
            
            Console.WriteLine($"Logging to {fileName}: {formattedLog}");
            
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
                string windowMonitorFileName = GetCurrentFileName();

                var logs = new List<ActiveWindowLog>();
                
               
                Console.WriteLine($"Reading mouse logs for: {date:yyyy-MM-dd}");
                string content = await _fileStorageService.ReadFileAsyncIfExists(windowMonitorFileName);
                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine($"No mouse logs found for: {date:yyyy-MM-dd}");
                    return logs;
                }

                var windowMonitorLogs = ParseLogsFromContent(content, date);
                logs.AddRange(windowMonitorLogs);            
                
                return logs;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Error retrieving logs: {ex.Message}", ex);
            }
        }

        private static IEnumerable<ActiveWindowLog> ParseLogsFromContent(string content, DateTime fileDate)
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
                        Console.WriteLine($"Skipping malformed log line (structure): {line}");
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
                                Console.WriteLine($"Skipping log line with invalid window handle '{parts[0]}': {line}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Skipping log line with incorrect number of content parts ({parts.Length} instead of {expectedParts}) '{logContent}': {line}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Skipping log line with invalid timestamp format '{timestampStr}': {line}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing log line '{line}': {ex.Message}");
                }

                if (log != null)
                    yield return log;
            }
        }

        public void MarkFileAsDeleted(DateTime date)
        {
            try
            {
                string windowMonitorFileName = GetCurrentFileName();

                bool windowMonitorExists = _fileStorageService.CheckIfFileExists(windowMonitorFileName);

                if (!windowMonitorExists)
                {
                    throw new FileNotFoundException($"No log files found for date: {date:yyyy-MM-dd}");
                }

                RenameToDeleted(windowMonitorFileName);
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Error marking files as deleted: {ex.Message}", ex);
            }
        }

        private void RenameToDeleted(string fileName)
        {
            try
            {
                // Create logs-deleted directory if it doesn't exist
                string deletedDir = "logs-deleted";
                _fileStorageService.EnsureDirectoryExists(deletedDir);

                string newFileName = Path.Combine(deletedDir, fileName);
                
                // If file already exists in deleted directory, append timestamp to make it unique
                if (_fileStorageService.CheckIfFileExists(newFileName))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    newFileName = Path.Combine(deletedDir, $"{fileNameWithoutExt}-{timestamp}{ext}");
                }

                // Move the file to the deleted directory
                _fileStorageService.RenameFile(fileName, newFileName);
                
                Console.WriteLine($"Successfully moved {fileName} to {newFileName}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error moving file {fileName} to deleted directory: {ex.Message}", ex);
            }
        }
    }
}