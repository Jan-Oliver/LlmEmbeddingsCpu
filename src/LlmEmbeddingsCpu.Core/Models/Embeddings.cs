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
        public required float[] Vector { get; set; }
        /// <summary>
        /// Gets or sets the name of the model used to generate the embedding.
        /// </summary>
        public required string ModelName { get; set; }
        /// <summary>
        /// Gets or sets the type of keyboard input that was the source of the embedding.
        /// </summary>
        public required KeyboardInputType KeyboardInputType { get; set; }
        /// <summary>
        /// Gets or sets the timestamp of the source input.
        /// </summary>
        public required DateTime Timestamp { get; set; }
        /// <summary>
        /// Gets or sets the content of the source input.
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>
        /// Returns a string representation of the embedding with all its properties.
        /// </summary>
        /// <returns>A formatted string containing all embedding properties.</returns>
        public override string ToString()
        {
            var vectorSummary = Vector?.Length > 0 
                ? $"[{Vector.Length} elements: {Vector[0]:F6}, {(Vector.Length > 1 ? Vector[1].ToString("F6") : "...")}, ...]"
                : "null";
            
            return $"Embedding {{ Id: {Id}, ModelName: '{ModelName}', KeyboardInputType: {KeyboardInputType}, " +
                   $"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}, Vector: {vectorSummary}, " +
                   $"OriginalText: '{OriginalText}' }}";
        }
    }
}