using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Data.FileStorage
{
    public class FileStorageService
    {
        private readonly string _basePath;

        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(string basePath, ILogger<FileStorageService> logger)
        {
            _logger = logger;

            if (string.IsNullOrEmpty(basePath))
            {
                _basePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "logs");
            }
            else
            {
                _basePath = Path.IsPathRooted(basePath) ? 
                    basePath : 
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, basePath);
            }
                
            // Create directory if it doesn't exist
            EnsureDirectoryExists(_basePath);
            
            _logger.LogInformation("Storing logs in: {BasePath}", _basePath);
        }

        public string GetFullPath(string filename)
        {
            return Path.Combine(_basePath, filename);
        }

        /// <summary>
        /// Asynchronously writes content to a file, either overwriting or appending.
        /// Uses File.WriteAllTextAsync or File.AppendAllTextAsync for non-blocking I/O.
        /// </summary>
        /// <param name="filename">The name of the file (relative to the base path).</param>
        /// <param name="content">The content to write.</param>
        /// <param name="append">If true, appends content; otherwise, overwrites.</param>
        public async Task WriteFileAsync(string filename, string content, bool append = false)
        {
            string fullPath = Path.Combine(_basePath, filename);

            try
            {
                if (append)
                {
                    await File.AppendAllTextAsync(fullPath, content);
                    _logger.LogInformation("Appended to file: {FilePath}", fullPath);
                }
                else
                {
                    await File.WriteAllTextAsync(fullPath, content);
                    _logger.LogInformation("Wrote to file: {FilePath}", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error writing to {FilePath}: {ErrorMessage}", fullPath, ex.Message);
            }
        }

        /// <summary>
        /// Asynchronously reads the entire text content from a file if it exists.
        /// Uses File.ReadAllTextAsync for non-blocking I/O.
        /// </summary>
        /// <param name="filename">The name of the file (relative to the base path).</param>
        /// <returns>The content of the file, or an empty string if the file does not exist or an error occurs.</returns>
        public async Task<string> ReadFileAsyncIfExists(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);

            if (!File.Exists(fullPath))
            {
                return string.Empty;
            }

            try
            {
                string content = await File.ReadAllTextAsync(fullPath);
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error reading from {FilePath}: {ErrorMessage}", fullPath, ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Lists files matching a pattern in the base directory.
        /// Directory.GetFiles is synchronous (no async counterpart).
        /// </summary>
        public IEnumerable<string> ListFiles(string pattern)
        {
            return Directory.GetFiles(_basePath, pattern)
                          .Select(f => Path.GetFileName(f));
        }

        public void MoveFile(string oldName, string newName)
        {
            string oldPath = Path.Combine(_basePath, oldName);
            string newPath = Path.Combine(_basePath, newName);

            if (!File.Exists(oldPath))
                throw new FileNotFoundException($"File not found: {oldName}", oldPath);

            if (File.Exists(newPath))
                throw new IOException($"File already exists: {newName}");

            try
            {
                File.Move(oldPath, newPath);
                _logger.LogInformation("Moved '{OldName}' to '{NewName}'", oldName, newName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error moving file from '{OldName}' to '{NewName}': {ErrorMessage}", oldName, newName, ex.Message);
                throw;
            }
        }

        public void MoveFolder(string oldFolderName, string newFolderName)
        {
            string oldPath = Path.Combine(_basePath, oldFolderName);
            string newPath = Path.Combine(_basePath, newFolderName);

            if (!Directory.Exists(oldPath))
                throw new DirectoryNotFoundException($"Directory not found: {oldFolderName}");

            if (Directory.Exists(newPath))
                throw new IOException($"Target directory already exists: {newFolderName}");

            try
            {
                Directory.Move(oldPath, newPath);
                _logger.LogInformation("Moved directory '{OldPath}' to '{NewPath}'", oldPath, newPath);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error moving directory from '{OldPath}' to '{NewPath}': {ErrorMessage}", oldPath, newPath, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Checks if a file exists. File.Exists is synchronous (no async counterpart).
        /// </summary>
        public bool CheckIfFileExists(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);
            return File.Exists(fullPath);
        }

        public bool CheckIfDirectoryExists(string directoryName)
        {
            string fullPath = Path.Combine(_basePath, directoryName);
            return Directory.Exists(fullPath);
        }

        public void DeleteFile(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);
            File.Delete(fullPath);
        }

        /// <summary>
        /// Ensures a subdirectory exists within the base path.
        /// Directory.Exists and Directory.CreateDirectory are synchronous (no async counterparts).
        /// </summary>
        public void EnsureDirectoryExists(string path)
        {
            string fullPath = Path.Combine(_basePath, path);

            try
            {
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    _logger.LogInformation("Ensured directory exists: {FilePath}", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create directory {FilePath}: {ErrorMessage}", fullPath, ex.Message);
                throw new InvalidOperationException($"Failed to ensure directory exists.", ex);
            }
        }
    }
}