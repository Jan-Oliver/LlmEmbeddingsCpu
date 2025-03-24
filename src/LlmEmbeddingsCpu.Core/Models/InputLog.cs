using System;
using LlmEmbeddingsCpu.Core.Enums;

namespace LlmEmbeddingsCpu.Core.Models
{
    public class InputLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Content { get; set; } = string.Empty;
        public InputType Type { get; set; }
    }
}