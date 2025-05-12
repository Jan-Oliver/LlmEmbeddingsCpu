namespace LlmEmbeddingsCpu.Data.FileStorage
{
    public class FileStorageService
    {
        private readonly string _basePath;

        public FileStorageService(string basePath)
        {
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
            
            Console.WriteLine($"Storing logs in: {_basePath}");
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
                    Console.WriteLine($"Appended to file: {fullPath}");
                }
                else
                {
                    await File.WriteAllTextAsync(fullPath, content);
                    Console.WriteLine($"Wrote to file: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error writing to {fullPath}: {ex.Message}");
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
                Console.Error.WriteLine($"Error reading from {fullPath}: {ex.Message}");
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

        /// <summary>
        /// Renames a file.
        /// Includes checks for existence.
        /// </summary>
        public void RenameFile(string oldName, string newName)
        {
            string oldPath = Path.Combine(_basePath, oldName);
            string newPath = Path.Combine(_basePath, newName);

            if (!File.Exists(oldPath))
            {
                throw new FileNotFoundException($"File not found: {oldName}", oldPath);
            }

            if (File.Exists(newPath))
            {
                throw new IOException($"File already exists: {newName}");
            }

            try
            {
                File.Move(oldPath, newPath);
                Console.WriteLine($"Renamed '{oldName}' to '{newName}'");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error renaming file from '{oldName}' to '{newName}': {ex.Message}");
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
                     Console.WriteLine($"Ensured directory exists: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to create directory {fullPath}: {ex.Message}");
                throw new InvalidOperationException($"Failed to ensure directory exists.", ex);
            }
        }
    }
}