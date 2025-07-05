using System.Windows.Forms;

namespace LlmEmbeddingsCpu.Core.Models
{
    /// <summary>
    /// Represents a log entry for an active window, capturing its handle, title, and process name at a specific time.
    /// </summary>
    public class ActiveWindowLog
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
        /// Gets or sets the handle of the active window.
        /// </summary>
        public IntPtr WindowHandle { get; set; }
        /// <summary>
        /// Gets or sets the title of the active window.
        /// </summary>
        public string WindowTitle { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the name of the process that owns the active window.
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

    }
}

