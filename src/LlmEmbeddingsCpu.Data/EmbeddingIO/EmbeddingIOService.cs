using LlmEmbeddingsCpu.Data.FileSystemIO;
using LlmEmbeddingsCpu.Core.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Data.EmbeddingIO
{
    /// <summary>
    /// Manages the I/O operations for embedding data.
    /// </summary>
    public class EmbeddingIOService(FileSystemIOService fileSystemIOService, ILogger<EmbeddingIOService> logger)
    {
        private readonly FileSystemIOService _fileSystemIOService = fileSystemIOService;
        private readonly string _embeddingDirectoryName = "embeddings";
        private readonly ILogger<EmbeddingIOService> _logger = logger;

        /// <summary>
        /// Gets the folder path for storing embeddings for a specific date.
        /// </summary>
        /// <param name="date">The date for which to get the folder path.</param>
        /// <returns>The folder path for the specified date.</returns>
        public string GetFolderPath(DateTime date)
        {
            string dateStr = date.ToString("yyyyMMdd");
            return Path.Combine(_embeddingDirectoryName, dateStr);
        }

        /// <summary>
        /// Asynchronously saves a single embedding to a file.
        /// </summary>
        /// <param name="embedding">The embedding to save.</param>
        /// <param name="date">The date to use for storing the embedding.</param>
        public async Task SaveEmbeddingAsync(Embedding embedding, DateTime date)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(embedding);

                // Create directory path and ensure it exists
                string datePath = GetFolderPath(date);
                _fileSystemIOService.EnsureDirectoryExists(datePath);

                // Create file name using Path.Combine for proper path handling
                string fileName = Path.Combine(datePath, $"{embedding.Id}.json");
                
                // Serialize the embedding to JSON
                string json = JsonConvert.SerializeObject(embedding, Formatting.Indented);
                
                // Save to file
                await _fileSystemIOService.WriteFileAsync(fileName, json);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save embedding {EmbeddingId}: {ErrorMessage}", embedding?.Id, ex.Message);
                throw new InvalidOperationException($"Failed to save embedding {embedding?.Id}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asynchronously saves a collection of embeddings.
        /// </summary>
        /// <param name="embeddings">The embeddings to save.</param>
        /// <param name="date">The date to use for storing the embeddings.</param>
        public async Task SaveEmbeddingsAsync(IEnumerable<Embedding> embeddings, DateTime date)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(embeddings);

                var errors = new List<Exception>();
                foreach (var embedding in embeddings)
                {
                    try
                    {
                        await SaveEmbeddingAsync(embedding, date);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                        _logger.LogError("Error saving embedding {EmbeddingId}: {ErrorMessage}", embedding.Id, ex.Message);
                    }
                }

                if (errors.Any())
                    throw new AggregateException("One or more embeddings failed to save", errors);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save embeddings batch: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException("Failed to save embeddings batch", ex);
            }
        }

        /// <summary>
        /// Asynchronously retrieves all embeddings for a given date.
        /// </summary>
        /// <param name="date">The date for which to retrieve embeddings.</param>
        /// <returns>An enumerable of embeddings for the specified date.</returns>
        public async Task<IEnumerable<Embedding>> GetEmbeddingsAsync(DateTime date)
        {
            try
            {
                // Create directory path for the given date
                string directoryPath = Path.Combine(_embeddingDirectoryName, date.ToString("yyyyMMdd"));
                
                var embeddings = new List<Embedding>();
                
                // Get all JSON files in the directory
                var files = _fileSystemIOService.ListFiles(Path.Combine(directoryPath, "*.json"));
                
                foreach (var file in files)
                {
                    try
                    {
                        string json = await _fileSystemIOService.ReadFileAsyncIfExists(file);
                        var embedding = JsonConvert.DeserializeObject<Embedding>(json);
                        if (embedding != null)
                        {
                            embeddings.Add(embedding);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error reading embedding file {FileName}: {ErrorMessage}", file, ex.Message);
                    }
                }
                
                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get embeddings for date {Date}: {ErrorMessage}", date, ex.Message);
                throw new InvalidOperationException($"Failed to get embeddings for date {date:yyyyMMdd}: {ex.Message}", ex);
            }
        }
    }
}