using System.Globalization;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileStorage;
using Microsoft.Extensions.Logging;
using LlmEmbeddingsCpu.Core.Enums;
using LlmEmbeddingsCpu.Common.Extensions;

namespace LlmEmbeddingsCpu.Data.KeyboardInputStorage
{
    public class KeyboardInputStorageService(
        FileStorageService fileStorageService,
        ILogger<KeyboardInputStorageService> logger)
    {
        private readonly FileStorageService _fileStorageService = fileStorageService;
        private readonly string _keyboardLogBaseFileName = "keyboard_logs";

        private readonly ILogger<KeyboardInputStorageService> _logger = logger;

        public string GetFilePath(DateTime date)
        {
            string timestamp = date.ToString("yyyy-MM-dd");
            return $"{_keyboardLogBaseFileName}-{timestamp}.txt";
        }

        public async Task SaveLogAsyncAndEncrypt(KeyboardInputLog log)
        {
            string fileName = GetFilePath(DateTime.Now);
            string encryptedContent = log.Content.ToRot13();
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.Type.ToString().ToLower()}|{encryptedContent}";
            
            _logger.LogInformation("Logging to {FileName}: {FormattedLog}", fileName, formattedLog);
            
            await _fileStorageService.WriteFileAsync(fileName, formattedLog + Environment.NewLine, true);
        }


        // Get all dates that have to be processed. Only the dates, not the files.
        public IEnumerable<DateTime> GetDatesToProcess()
        {
            var files = _fileStorageService.ListFiles("*.txt");
            var logFiles = files.Where(f =>  
                f.StartsWith(_keyboardLogBaseFileName))
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

        // Get all logs for a given date.
        public async Task<IEnumerable<KeyboardInputLog>> GetPreviousLogsAsyncDecrypted(DateTime date)
        {
            try
            {
                string keyboardFileName = $"{_keyboardLogBaseFileName}-{date:yyyy-MM-dd}.txt";

                
                var logs = new List<KeyboardInputLog>();
                
                
                _logger.LogInformation("Reading keyboard logs for: {Date}", date);
                string content = await _fileStorageService.ReadFileAsyncIfExists(keyboardFileName);

                if (string.IsNullOrEmpty(content))
                {
                    _logger.LogInformation("No keyboard logs found for: {Date}", date);
                    return logs;
                }

                var keyboardLogs = ParseKeyboardLogsFromContentAndDecrypt(content, date);
                logs.AddRange(keyboardLogs);

                return logs;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                _logger.LogError("Error retrieving logs: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Error retrieving logs: {ex.Message}", ex);
            }
        }

        // Parse the logs from the content.
        private static IEnumerable<KeyboardInputLog> ParseKeyboardLogsFromContentAndDecrypt(string content, DateTime fileDate)
        {
            if (string.IsNullOrEmpty(content))
                yield break;
            
            const string timestampFormat = "HH:mm:ss";
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Parse line format: [HH:mm:ss] Content
                if (line.Length > 10 && line[0] == '[' && line[9] == ']')
                {
                    string timestampStr = line[1..9];
                    string remainder    = line[10..].Trim();

                    if (DateTime.TryParseExact(timestampStr, timestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timeOnly))
                    {
                        // split "special|ctrl+alt+n"
                        int pipe = remainder.IndexOf('|');
                        if (pipe <= 0) continue;          // malformed line – skip

                        string eventStr = remainder[..pipe];
                        string payload  = remainder[(pipe + 1)..];
                        string decryptedPayload = payload.FromRot13();

                        if (!Enum.TryParse<KeyboardInputType>(eventStr, true, out var type))
                            continue;                    // unknown event – skip

                        DateTime timestamp = fileDate.Date
                                    .AddHours (timeOnly.Hour)
                                    .AddMinutes(timeOnly.Minute)
                                    .AddSeconds(timeOnly.Second);

                        yield return new KeyboardInputLog
                        {
                            Timestamp = timestamp,
                            Type = type,
                            Content = decryptedPayload,
                        };
                    }
                }
            }
        }

        // Mark the file as deleted.
        public void MarkFileAsDeleted(DateTime date)
        {
            try
            {
                string keyboardFileName = GetFilePath(date);

                bool keyboardFileExists = _fileStorageService.CheckIfFileExists(keyboardFileName);

                if (!keyboardFileExists)
                {
                    _logger.LogError("No log files found for date: {Date}", date);
                    return;
                }

                RenameToDeleted(keyboardFileName);
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                _logger.LogError("Error marking files as deleted: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Error marking files as deleted: {ex.Message}", ex);
            }
        }

        // Rename the file to the deleted directory.
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
                _fileStorageService.MoveFile(fileName, newFileName);
                
                _logger.LogInformation("Successfully moved {FileName} to {NewFileName}", fileName, newFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error moving file {FileName} to deleted directory: {ErrorMessage}", fileName, ex.Message);
                throw new InvalidOperationException($"Error moving file {fileName} to deleted directory: {ex.Message}", ex);
            }
        }
    }
}