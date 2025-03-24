using System.Collections.Generic;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Models;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IEmbeddingService
    {
        Task<Embedding> GenerateEmbeddingAsync(string text);
        Task<IEnumerable<Embedding>> GenerateEmbeddingsAsync(IEnumerable<string> texts);
    }
}