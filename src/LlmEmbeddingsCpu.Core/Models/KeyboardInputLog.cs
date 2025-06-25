using LlmEmbeddingsCpu.Core.Enums;

namespace LlmEmbeddingsCpu.Core.Models
{
    /// <summary>
    /// Represents a log entry for keyboard input, capturing the content and type of input.
    /// </summary>
    public class KeyboardInputLog
    {
        /// <summary>
        /// Gets or sets the unique identifier for the log entry.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// Gets or sets the timestamp when the log was created.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        /// <summary>
        /// Gets or sets the textual content of the keyboard input.
        /// </summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the type of keyboard input (e.g., Text or Special).
        /// </summary>
        public KeyboardInputType Type { get; set; }
    }
}