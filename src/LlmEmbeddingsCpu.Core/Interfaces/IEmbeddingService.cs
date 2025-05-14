using LlmEmbeddingsCpu.Core.Models;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IEmbeddingService
    {
        Task<Core.Models.Embedding> GenerateEmbeddingAsync(KeyboardInputLog keyboardInputLog);
        Task<IEnumerable<Core.Models.Embedding>> GenerateEmbeddingsAsync(IEnumerable<KeyboardInputLog> keyboardInputLogs);
    }
}
