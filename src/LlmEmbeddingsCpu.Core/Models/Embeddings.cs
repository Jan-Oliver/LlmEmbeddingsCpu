using System;

namespace LlmEmbeddingsCpu.Core.Models
{
    public class Embedding
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SourceText { get; set; } = string.Empty;
        public float[] Vector { get; set; } = Array.Empty<float>();
        public string ModelName { get; set; } = string.Empty;
    }
}