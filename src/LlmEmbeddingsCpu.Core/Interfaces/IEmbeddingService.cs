namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IEmbeddingService
    {
        Task<Core.Models.Embedding> GenerateEmbeddingAsync(string text);
        Task<IEnumerable<Core.Models.Embedding>> GenerateEmbeddingsAsync(IEnumerable<string> texts);
        
        void Dispose();
    }
}
