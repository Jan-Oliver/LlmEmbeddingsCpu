using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileStorage;

namespace LlmEmbeddingsCpu.Data.MouseInputStorage
{
    public class MouseInputStorageService(FileStorageService fileStorageService)
    {
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly string _mouseLogBaseFileName = "mouse_logs";

        private string GetCurrentFileName()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd");
            return $"{_mouseLogBaseFileName}-{timestamp}.txt";
        }

        public async Task SaveLogAsync(MouseInputLog log)
        {
            string fileName = GetCurrentFileName();
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.Content}";
            
            Console.WriteLine($"Logging to {fileName}: {formattedLog}");
            
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
                string mouseInputFileName = GetCurrentFileName();

                var logs = new List<MouseInputLog>();
                
               
                Console.WriteLine($"Reading mouse logs for: {date:yyyy-MM-dd}");
                string content = await _fileStorageService.ReadFileAsyncIfExists(mouseInputFileName);
                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine($"No mouse logs found for: {date:yyyy-MM-dd}");
                    return logs;
                }

                var mouseLogs = ParseLogsFromContent(content);
                logs.AddRange(mouseLogs);            
                
                return logs;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Error retrieving logs: {ex.Message}", ex);
            }
        }

        private static IEnumerable<MouseInputLog> ParseLogsFromContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                yield break;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Parse line format: [HH:mm:ss] Content
                if (line.Length > 10 && line[0] == '[' && line[9] == ']')
                {
                    string timestampStr = line.Substring(1, 8);
                    string logContent = line.Substring(9).Trim();

                    if (DateTime.TryParse(timestampStr, out DateTime timestamp))
                    {
                        yield return new MouseInputLog
                        {
                            Content = logContent,
                            Timestamp = timestamp,
                        };
                    }
                }
            }
        }

        public async Task MarkFileAsDeleted(DateTime date)
        {
            try
            {
                string mouseFileName = GetCurrentFileName();

                bool mouseExists = _fileStorageService.CheckIfFileExists(mouseFileName);

                if (!mouseExists)
                {
                    throw new FileNotFoundException($"No log files found for date: {date:yyyy-MM-dd}");
                }

                RenameToDeleted(mouseFileName);
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