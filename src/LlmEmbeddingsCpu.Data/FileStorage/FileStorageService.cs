using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using LlmEmbeddingsCpu.Core.Interfaces;

namespace LlmEmbeddingsCpu.Data.FileStorage
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _basePath;

        public FileStorageService(string basePath = null)
        {
            if (basePath == null)
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
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
            
            Console.WriteLine($"Storing logs in: {_basePath}");
        }

        public async Task WriteTextAsync(string filename, string content, bool append = false)
        {
            string fullPath = Path.Combine(_basePath, filename);
            
            try
            {
                if (append)
                {
                    await File.AppendAllTextAsync(fullPath, content);
                }
                else
                {
                    await File.WriteAllTextAsync(fullPath, content);
                }
                
                Console.WriteLine($"Wrote to file: {fullPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to {fullPath}: {ex.Message}");
            }
        }

        public async Task<string> ReadTextAsync(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);
            
            try
            {
                if (File.Exists(fullPath))
                {
                    return await File.ReadAllTextAsync(fullPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from {fullPath}: {ex.Message}");
            }
            
            return string.Empty;
        }

        public bool FileExists(string filename)
        {
            string fullPath = Path.Combine(_basePath, filename);
            return File.Exists(fullPath);
        }

        public IEnumerable<string> ListFiles(string pattern)
        {
            return Directory.GetFiles(_basePath, pattern)
                          .Select(f => Path.GetFileName(f));
        }

        public void RenameFile(string oldName, string newName)
        {
            string oldPath = Path.Combine(_basePath, oldName);
            string newPath = Path.Combine(_basePath, newName);
            
            if (!File.Exists(oldPath))
            {
                throw new FileNotFoundException($"File not found: {oldName}");
            }

            if (File.Exists(newPath))
            {
                throw new IOException($"File already exists: {newName}");
            }

            File.Move(oldPath, newPath);
        }

        public async Task EnsureDirectoryExistsAsync(string path)
        {
            string fullPath = Path.Combine(_basePath, path);
            
            try
            {
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create directory {fullPath}: {ex.Message}", ex);
            }
        }
    }
}