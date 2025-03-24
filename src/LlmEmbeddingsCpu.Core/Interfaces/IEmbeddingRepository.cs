using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Models;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IEmbeddingRepository
    {
        Task SaveEmbeddingAsync(Embedding embedding);
        Task SaveEmbeddingsAsync(IEnumerable<Embedding> embeddings);
        Task<IEnumerable<Embedding>> GetEmbeddingsAsync(DateTime date);
    }
}