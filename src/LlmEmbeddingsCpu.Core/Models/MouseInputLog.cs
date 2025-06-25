using System.Windows.Forms;

namespace LlmEmbeddingsCpu.Core.Models
{
    /// <summary>
    /// Represents a log entry for a mouse click event.
    /// </summary>
    public class MouseInputLog
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
        /// Gets or sets the <see cref="MouseEventArgs"/> associated with the mouse click.
        /// </summary>
        public MouseEventArgs Content { get; set; } = new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0);
    }
}

