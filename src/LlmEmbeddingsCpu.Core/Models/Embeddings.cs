using LlmEmbeddingsCpu.Core.Enums;

namespace LlmEmbeddingsCpu.Core.Models
{
    /// <summary>
    /// Represents a text embedding, including the vector, model information, and source data.
    /// </summary>
    public class Embedding
    {
        /// <summary>
        /// Gets or sets the unique identifier for the embedding.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// Gets or sets the numerical vector representing the embedding.
        /// </summary>
        public float[] Vector { get; set; } = Array.Empty<float>();
        /// <summary>
        /// Gets or sets the name of the model used to generate the embedding.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the type of keyboard input that was the source of the embedding.
        /// </summary>
        public KeyboardInputType KeyboardInputType { get; set; }
        /// <summary>
        /// Gets or sets the timestamp of the source input.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}