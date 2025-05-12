using System.Windows.Forms;

namespace LlmEmbeddingsCpu.Core.Models
{
    public class ActiveWindowLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;

    }
}

