namespace LlmEmbeddingsCpu.Core.Models
{
    public class MouseInputLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Content { get; set; } = string.Empty;
    }
}