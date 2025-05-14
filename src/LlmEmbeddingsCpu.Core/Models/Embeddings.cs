using LlmEmbeddingsCpu.Core.Enums;

namespace LlmEmbeddingsCpu.Core.Models
{
    public class Embedding
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public float[] Vector { get; set; } = Array.Empty<float>();
        public string ModelName { get; set; } = string.Empty;
        public KeyboardInputType KeyboardInputType { get; set; }
        public DateTime Timestamp { get; set; }

        public string SourceText { get; set; } = string.Empty;
    }
}