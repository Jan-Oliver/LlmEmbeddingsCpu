using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Core.Enums;
using Newtonsoft.Json;
using System.Linq;
using System.IO;

namespace LlmEmbeddingsCpu.Data.Repositories
{
    public class InputLogRepository : IInputLogRepository
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly string _keyboardLogBaseFileName;
        private readonly string _mouseLogBaseFileName;

        public InputLogRepository(IFileStorageService fileStorageService)
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

        public async Task<IEnumerable<InputLog>> GetPreviousLogsAsync(DateTime? date = null)
        {
            try
            {
                if (date.HasValue)
                {
                    string keyboardFileName = $"{_keyboardLogBaseFileName}-{date.Value:yyyy-MM-dd}.txt";
                    string mouseFileName = $"{_mouseLogBaseFileName}-{date.Value:yyyy-MM-dd}.txt";

                    bool keyboardExists = _fileStorageService.FileExists(keyboardFileName);
                    bool mouseExists = _fileStorageService.FileExists(mouseFileName);

                    if (!keyboardExists && !mouseExists)
                    {
                        throw new FileNotFoundException($"No log files found for date: {date.Value:yyyy-MM-dd}");
                    }

                    var logs = new List<InputLog>();
                    
                    if (keyboardExists)
                    {
                        Console.WriteLine($"Reading keyboard logs for: {date.Value:yyyy-MM-dd}");
                        string content = await _fileStorageService.ReadTextAsync(keyboardFileName);
                        var keyboardLogs = ParseLogsFromContent(content, InputType.Keyboard);
                        logs.AddRange(keyboardLogs);
                    }
                    
                    if (mouseExists)
                    {
                        Console.WriteLine($"Reading mouse logs for: {date.Value:yyyy-MM-dd}");
                        string content = await _fileStorageService.ReadTextAsync(mouseFileName);
                        var mouseLogs = ParseLogsFromContent(content, InputType.Mouse);
                        logs.AddRange(mouseLogs);
                    }
                    
                    return logs;
                }
                else
                {
                    var files = _fileStorageService.ListFiles("*.txt");
                    var logFiles = files.Where(f => !f.StartsWith("deleted-") && 
                        (f.StartsWith(_keyboardLogBaseFileName) || f.StartsWith(_mouseLogBaseFileName)))
                        .OrderBy(f => f);

                    if (!logFiles.Any())
                    {
                        throw new FileNotFoundException("No log files available");
                    }

                    // Get the oldest date from the filenames
                    var oldestFile = logFiles.First();
                    var dateStr = oldestFile.Split('-')[1].Replace(".txt", "");
                    var oldestDate = DateTime.ParseExact(dateStr, "yyyy-MM-dd", null);

                    // Recursive call with the oldest date
                    return await GetPreviousLogsAsync(oldestDate);
                }
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Error retrieving logs: {ex.Message}", ex);
            }
        }

        private IEnumerable<InputLog> ParseLogsFromContent(string content, InputType type)
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

        public void MarkFileAsDeleted(DateTime? date = null)
        {
            try
            {
                if (date.HasValue)
                {
                    string keyboardFileName = $"{_keyboardLogBaseFileName}-{date.Value:yyyy-MM-dd}.txt";
                    string mouseFileName = $"{_mouseLogBaseFileName}-{date.Value:yyyy-MM-dd}.txt";

                    bool keyboardExists = _fileStorageService.FileExists(keyboardFileName);
                    bool mouseExists = _fileStorageService.FileExists(mouseFileName);

                    if (!keyboardExists && !mouseExists)
                    {
                        throw new FileNotFoundException($"No log files found for date: {date.Value:yyyy-MM-dd}");
                    }

                    if (keyboardExists)
                        RenameToDeleted(keyboardFileName);
                    if (mouseExists)
                        RenameToDeleted(mouseFileName);
                }
                else
                {
                    var files = _fileStorageService.ListFiles("*.txt");
                    var logFiles = files.Where(f => !f.StartsWith("deleted-") && 
                        (f.StartsWith(_keyboardLogBaseFileName) || f.StartsWith(_mouseLogBaseFileName)))
                        .OrderBy(f => f);

                    if (!logFiles.Any())
                    {
                        throw new FileNotFoundException("No log files available to mark as deleted");
                    }

                    RenameToDeleted(logFiles.First());
                }
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
                string newFileName = $"deleted-{fileName}";
                if (_fileStorageService.FileExists(newFileName))
                {
                    throw new InvalidOperationException($"Deleted file already exists: {newFileName}");
                }

                _fileStorageService.RenameFile(fileName, newFileName);
                Console.WriteLine($"Successfully renamed {fileName} to {newFileName}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error renaming file {fileName}: {ex.Message}", ex);
            }
        }
    }
}