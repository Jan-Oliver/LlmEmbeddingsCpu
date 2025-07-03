using System.Globalization;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.FileSystemIO;
using Microsoft.Extensions.Logging;
using LlmEmbeddingsCpu.Core.Enums;
using LlmEmbeddingsCpu.Common.Extensions;

namespace LlmEmbeddingsCpu.Data.KeyboardLogIO
{
    /// <summary>
    /// Manages the I/O operations for keyboard input logs.
    /// Handles encryption, file naming, and log parsing.
    /// </summary>
    public class KeyboardLogIOService(
        FileSystemIOService fileSystemIOService,
        ILogger<KeyboardLogIOService> logger)
    {
        private readonly FileSystemIOService _fileSystemIOService = fileSystemIOService;
        private readonly string _keyboardLogBaseFileName = "keyboard_logs";

        private readonly ILogger<KeyboardLogIOService> _logger = logger;

        /// <summary>
        /// Generates the file path for a keyboard log file based on a given date.
        /// </summary>
        /// <param name="date">The date for which to generate the file path.</param>
        /// <returns>The formatted file path string.</returns>
        public string GetFilePath(DateTime date)
        {
            string timestamp = date.ToString("yyyyMMdd");
            return $"{_keyboardLogBaseFileName}-{timestamp}.txt";
        }

        /// <summary>
        /// Saves a keyboard input log to a file, encrypting the content before writing.
        /// </summary>
        /// <param name="log">The keyboard input log to save.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SaveLogAsyncAndEncrypt(KeyboardInputLog log)
        {
            string fileName = GetFilePath(DateTime.Now);
            string encryptedContent = log.Content.ToRot13();
            string formattedLog = $"[{log.Timestamp:HH:mm:ss}] {log.Type.ToString().ToLower()}|{encryptedContent}";
            
            _logger.LogInformation("Logging to {FileName}: {FormattedLog}", fileName, formattedLog);
            
            await _fileSystemIOService.WriteFileAsync(fileName, formattedLog + Environment.NewLine, true);
        }


        /// <summary>
        /// Retrieves a collection of unique dates for which keyboard log files exist and need to be processed.
        /// </summary>
        /// <remarks>
        /// This method filters for log files from dates before the current day.
        /// </remarks>
        /// <returns>An <see cref="IEnumerable{DateTime}"/> containing the dates to be processed.</returns>
        public IEnumerable<DateTime> GetDatesToProcess()
        {
            var files = _fileSystemIOService.ListFiles("*.txt");
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

        /// <summary>
        /// Retrieves and decrypts all keyboard input logs for a specific date from the corresponding log file.
        /// </summary>
        /// <param name="date">The date for which to retrieve the logs.</param>
        /// <returns>A <see cref="Task{IEnumerable{KeyboardInputLog}}"/> containing the decrypted logs for the specified date.</returns>
        public async Task<IEnumerable<KeyboardInputLog>> GetPreviousLogsAsyncDecrypted(DateTime date)
        {
            try
            {
                string keyboardFileName = $"{_keyboardLogBaseFileName}-{date:yyyyMMdd}.txt";

                
                var logs = new List<KeyboardInputLog>();
                
                
                _logger.LogDebug("Reading keyboard logs for: {Date}", date);
                string content = await _fileSystemIOService.ReadFileAsyncIfExists(keyboardFileName);

                if (string.IsNullOrEmpty(content))
                {
                    _logger.LogDebug("No keyboard logs found for: {Date}", date);
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

        /// <summary>
        /// Parses keyboard log entries from a string content and decrypts them.
        /// </summary>
        /// <param name="content">The string content of the log file.</param>
        /// <param name="fileDate">The date of the log file, used to construct the full timestamp.</param>
        /// <returns>An <see cref="IEnumerable{KeyboardInputLog}"/> of the parsed and decrypted logs.</returns>
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

        /// <summary>
        /// Deletes the log file for a specific date.
        /// </summary>
        /// <param name="date">The date of the log file to be deleted.</param>
        public void DeleteFile(DateTime date)
        {
            try
            {
                string keyboardFileName = GetFilePath(date);

                bool keyboardFileExists = _fileSystemIOService.CheckIfFileExists(keyboardFileName);

                if (!keyboardFileExists)
                {
                    _logger.LogError("No log files found for date: {Date}", date);
                    return;
                }

                _fileSystemIOService.DeleteFile(keyboardFileName);
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                _logger.LogError("Error deleting file: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Error deleting file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Debug only:Marks the log file for a specific date as deleted by moving it to a designated 'deleted' directory.
        /// </summary>
        /// <param name="date">The date of the log file to be marked as deleted.</param>
        public void MarkFileAsDeleted(DateTime date)
        {
            try
            {
                string keyboardFileName = GetFilePath(date);

                bool keyboardFileExists = _fileSystemIOService.CheckIfFileExists(keyboardFileName);

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

        /// <summary>
        /// Renames and moves a given log file to the 'logs-deleted' directory.
        /// </summary>
        /// <remarks>
        /// If a file with the same name already exists in the destination, it appends a timestamp to ensure uniqueness.
        /// </remarks>
        /// <param name="fileName">The name of the file to move.</param>
        private void RenameToDeleted(string fileName)
        {
            try
            {
                // Create logs-deleted directory if it doesn't exist
                string deletedDir = "logs-deleted";
                _fileSystemIOService.EnsureDirectoryExists(deletedDir);

                string newFileName = Path.Combine(deletedDir, fileName);
                
                // If file already exists in deleted directory, append timestamp to make it unique
                if (_fileSystemIOService.CheckIfFileExists(newFileName))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    newFileName = Path.Combine(deletedDir, $"{fileNameWithoutExt}-{timestamp}{ext}");
                }

                // Move the file to the deleted directory
                _fileSystemIOService.MoveFile(fileName, newFileName);
                
                _logger.LogDebug("Successfully moved {FileName} to {NewFileName}", fileName, newFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error moving file {FileName} to deleted directory: {ErrorMessage}", fileName, ex.Message);
                throw new InvalidOperationException($"Error moving file {fileName} to deleted directory: {ex.Message}", ex);
            }
        }
    }
}