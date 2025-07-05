using LlmEmbeddingsCpu.Core.Models;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that generates text embeddings.
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Asynchronously generates an embedding for a single keyboard input log.
        /// </summary>
        /// <param name="keyboardInputLog">The keyboard input log to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the generated <see cref="Embedding"/>.</returns>
        Task<Core.Models.Embedding> GenerateEmbeddingAsync(KeyboardInputLog keyboardInputLog);
        /// <summary>
        /// Asynchronously generates embeddings for a collection of keyboard input logs.
        /// </summary>
        /// <param name="keyboardInputLogs">The collection of keyboard input logs to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable of generated <see cref="Embedding"/>s.</returns>
        Task<IEnumerable<Core.Models.Embedding>> GenerateEmbeddingsAsync(IEnumerable<KeyboardInputLog> keyboardInputLogs);
    }
}
