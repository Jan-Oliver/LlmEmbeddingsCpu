using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using Newtonsoft.Json;

namespace LlmEmbeddingsCpu.Data.Repositories
{
    public class EmbeddingRepository : IEmbeddingRepository
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly string _embeddingDirectoryName;

        public EmbeddingRepository(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
            _embeddingDirectoryName = "embeddings";
        }

        public async Task SaveEmbeddingAsync(Embedding embedding)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(embedding);

                // Create directory path and ensure it exists
                string datePath = Path.Combine(_embeddingDirectoryName, embedding.CreatedAt.ToString("yyyy-MM-dd"));
                await _fileStorageService.EnsureDirectoryExistsAsync(datePath);

                // Create file name using Path.Combine for proper path handling
                string fileName = Path.Combine(datePath, $"{embedding.Id}.json");
                
                // Serialize the embedding to JSON
                string json = JsonConvert.SerializeObject(embedding, Formatting.Indented);
                
                // Save to file
                await _fileStorageService.WriteTextAsync(fileName, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save embedding {embedding?.Id}: {ex.Message}", ex);
            }
        }

        public async Task SaveEmbeddingsAsync(IEnumerable<Embedding> embeddings)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(embeddings);

                var errors = new List<Exception>();
                foreach (var embedding in embeddings)
                {
                    try
                    {
                        await SaveEmbeddingAsync(embedding);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                        Console.WriteLine($"Error saving embedding {embedding.Id}: {ex.Message}");
                    }
                }

                if (errors.Any())
                    throw new AggregateException("One or more embeddings failed to save", errors);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to save embeddings batch", ex);
            }
        }

        public async Task<IEnumerable<Embedding>> GetEmbeddingsAsync(DateTime date)
        {
            try
            {
                // Create directory path for the given date
                string directoryPath = Path.Combine(_embeddingDirectoryName, date.ToString("yyyy-MM-dd"));
                
                var embeddings = new List<Embedding>();
                
                // Get all JSON files in the directory
                var files = _fileStorageService.ListFiles(Path.Combine(directoryPath, "*.json"));
                
                foreach (var file in files)
                {
                    try
                    {
                        string json = await _fileStorageService.ReadTextAsync(file);
                        var embedding = JsonConvert.DeserializeObject<Embedding>(json);
                        if (embedding != null)
                        {
                            embeddings.Add(embedding);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading embedding file {file}: {ex.Message}");
                    }
                }
                
                return embeddings;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get embeddings for date {date:yyyy-MM-dd}: {ex.Message}", ex);
            }
        }
    }
}