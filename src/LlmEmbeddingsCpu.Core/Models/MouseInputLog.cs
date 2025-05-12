using System.Windows.Forms;

namespace LlmEmbeddingsCpu.Core.Models
{
    public class MouseInputLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public MouseEventArgs Content { get; set; } = new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0);
    }
}

