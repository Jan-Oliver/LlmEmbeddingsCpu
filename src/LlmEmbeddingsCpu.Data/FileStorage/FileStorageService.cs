using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Data.FileStorage
{
    /// <summary>
    /// Provides services for file storage operations such as reading, writing, and moving files.
    /// </summary>
    public class FileStorageService
    {
        private readonly string _basePath;

        private readonly ILogger<FileStorageService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorageService"/> class.
        /// </summary>
        /// <param name="basePath">The base path for file storage. If not provided, a default 'logs' directory is used.</param>
        /// <param name="logger">The logger instance for logging messages.</param>
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

        /// <summary>
        /// Gets the full path for a given filename within the base storage directory.
        /// </summary>
        /// <param name="filename">The name of the file.</param>
        /// <returns>The full path to the file.</returns>
        public string GetFullPath(string filename)
        {
            return Path.Combine(_basePath, filename);
        }

        /// <summary>
        /// Asynchronously writes content to a file, either overwriting or appending.
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
        /// </summary>
        /// <param name="pattern">The search pattern to match against the names of files.</param>
        /// <returns>An enumerable collection of the full names (including paths) for the files in the directory that match the specified search pattern.</returns>
        public IEnumerable<string> ListFiles(string pattern)
        {
            return Directory.GetFiles(_basePath, pattern)
                          .Select(f => Path.GetFileName(f));
        }

        /// <summary>
        /// Moves a file to a new location.
        /// </summary>
        /// <param name="oldName">The name of the file to move.</param>
        /// <param name="newName">The new name for the file.</param>
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

        /// <summary>
        /// Moves a folder to a new location.
        /// </summary>
        /// <param name="oldFolderName">The name of the folder to move.</param>
        /// <param name="newFolderName">The new name for the folder.</param>
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
        /// Checks if a file exists.
        /// </summary>
        /// <param name="filename">The name of the file to check.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public bool CheckIfFileExists(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);
            return File.Exists(fullPath);
        }

        /// <summary>
        /// Checks if a directory exists.
        /// </summary>
        /// <param name="directoryName">The name of the directory to check.</param>
        /// <returns>True if the directory exists; otherwise, false.</returns>
        public bool CheckIfDirectoryExists(string directoryName)
        {
            string fullPath = Path.Combine(_basePath, directoryName);
            return Directory.Exists(fullPath);
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="filename">The name of the file to be deleted.</param>
        public void DeleteFile(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);
            File.Delete(fullPath);
        }

        /// <summary>
        /// Ensures that a directory exists, creating it if it does not.
        /// </summary>
        /// <param name="path">The path of the directory to check and create.</param>
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