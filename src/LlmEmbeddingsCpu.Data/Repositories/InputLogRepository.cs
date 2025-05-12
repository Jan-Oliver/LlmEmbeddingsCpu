using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Core.Enums;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
using LlmEmbeddingsCpu.Data.FileStorage;

namespace LlmEmbeddingsCpu.Data.Repositories
{
    public class InputLogRepository
    {
        private readonly FileStorageService _fileStorageService;
        private readonly string _keyboardLogBaseFileName;
        private readonly string _mouseLogBaseFileName;

        public InputLogRepository(FileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
            _keyboardLogBaseFileName = "keyboard_logs";
            _mouseLogBaseFileName = "mouse_logs";
        }

        private string GetCurrentFileName(InputType type)
        {
            string baseFileName = type == InputType.Keyboard ? _keyboardLogBaseFileName : _mouseLogBaseFileName;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd");
            return $"{baseFileName}-{timestamp}.txt";
        }

        public async Task SaveLogAsync(InputLog log)
        {
            string fileName = GetCurrentFileName(log.Type);
            string formattedLog = $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.Content}";
            
            Console.WriteLine($"Logging to {fileName}: {formattedLog}");
            
            await _fileStorageService.WriteTextAsync(fileName, formattedLog + Environment.NewLine, true);
        }

        public IEnumerable<DateTime> GetDatesToProcess()
        {
            var files = _fileStorageService.ListFiles("*.txt");
            var logFiles = files.Where(f =>  
                f.StartsWith(_keyboardLogBaseFileName) || f.StartsWith(_mouseLogBaseFileName))
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

        public async Task<IEnumerable<InputLog>> GetPreviousLogsAsync(DateTime date)
        {
            try
            {
                string keyboardFileName = $"{_keyboardLogBaseFileName}-{date:yyyy-MM-dd}.txt";
                string mouseFileName = $"{_mouseLogBaseFileName}-{date:yyyy-MM-dd}.txt";

                bool keyboardExists = _fileStorageService.FileExists(keyboardFileName);
                bool mouseExists = _fileStorageService.FileExists(mouseFileName);

                if (!keyboardExists && !mouseExists)
                {
                    throw new FileNotFoundException($"No log files found for date: {date:yyyy-MM-dd}");
                }

                var logs = new List<InputLog>();
                
                if (keyboardExists)
                {
                    Console.WriteLine($"Reading keyboard logs for: {date:yyyy-MM-dd}");
                    string content = await _fileStorageService.ReadTextAsync(keyboardFileName);
                    var keyboardLogs = ParseLogsFromContent(content, InputType.Keyboard);
                    logs.AddRange(keyboardLogs);
                }
                
                if (mouseExists)
                {
                    Console.WriteLine($"Reading mouse logs for: {date:yyyy-MM-dd}");
                    string content = await _fileStorageService.ReadTextAsync(mouseFileName);
                    var mouseLogs = ParseLogsFromContent(content, InputType.Mouse);
                    logs.AddRange(mouseLogs);
                }
                
                return logs;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Error retrieving logs: {ex.Message}", ex);
            }
        }

        private static IEnumerable<InputLog> ParseLogsFromContent(string content, InputType type)
        {
            if (string.IsNullOrEmpty(content))
                yield break;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Parse line format: [yyyy-MM-dd HH:mm:ss] Content
                if (line.Length > 21 && line[0] == '[' && line[20] == ']')
                {
                    string timestampStr = line.Substring(1, 19);
                    string logContent = line.Substring(22).Trim();

                    if (DateTime.TryParse(timestampStr, out DateTime timestamp))
                    {
                        yield return new InputLog
                        {
                            Content = logContent,
                            Timestamp = timestamp,
                            Type = type
                        };
                    }
                }
            }
        }

        public async Task MarkFileAsDeleted(DateTime date)
        {
            try
            {
                string keyboardFileName = $"{_keyboardLogBaseFileName}-{date:yyyy-MM-dd}.txt";
                string mouseFileName = $"{_mouseLogBaseFileName}-{date:yyyy-MM-dd}.txt";

                bool keyboardExists = _fileStorageService.FileExists(keyboardFileName);
                bool mouseExists = _fileStorageService.FileExists(mouseFileName);

                if (!keyboardExists && !mouseExists)
                {
                    throw new FileNotFoundException($"No log files found for date: {date:yyyy-MM-dd}");
                }

                if (keyboardExists)
                    await RenameToDeleted(keyboardFileName);
                if (mouseExists)
                    await RenameToDeleted(mouseFileName);
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Error marking files as deleted: {ex.Message}", ex);
            }
        }

        private async Task RenameToDeleted(string fileName)
        {
            try
            {
                // Create logs-deleted directory if it doesn't exist
                string deletedDir = "logs-deleted";
                await _fileStorageService.EnsureDirectoryExistsAsync(deletedDir);

                string newFileName = Path.Combine(deletedDir, fileName);
                
                // If file already exists in deleted directory, append timestamp to make it unique
                if (_fileStorageService.FileExists(newFileName))
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