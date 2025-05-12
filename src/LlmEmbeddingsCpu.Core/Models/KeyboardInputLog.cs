namespace LlmEmbeddingsCpu.Core.Models
{
    public class KeyboardInputLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Content { get; set; } = string.Empty;
    }
}