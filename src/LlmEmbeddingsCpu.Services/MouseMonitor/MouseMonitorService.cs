using Gma.System.MouseKeyHook;
using System.Windows.Forms;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.MouseLogIO;
using Microsoft.Extensions.Logging;

namespace LlmEmbeddingsCpu.Services.MouseMonitor
{
    /// <summary>
    /// Monitors global mouse click events and logs them.
    /// </summary>
    public class MouseMonitorService(
        MouseLogIOService mouseInputStorageService,
        ILogger<MouseMonitorService> logger)
    {
        private IMouseEvents? _globalHook;
        private readonly MouseLogIOService _mouseInputStorageService = mouseInputStorageService;
        private readonly ILogger<MouseMonitorService> _logger = logger;

        /// <summary>
        /// Starts monitoring global mouse click events.
        /// </summary>
        public void StartTracking()
        {
            // Subscribe to global mouse events
            _globalHook = Hook.GlobalEvents();
            _globalHook.MouseClick += GlobalHook_MouseClick;
            _logger.LogInformation("Mouse tracking started...");
        }
        
        /// <summary>
        /// Stops monitoring global mouse click events.
        /// </summary>
        public void StopTracking()
        {
            if (_globalHook != null)
            {
                _globalHook.MouseClick -= GlobalHook_MouseClick;
            }
            
            _logger.LogInformation("Mouse tracking stopped.");
        }

        /// <summary>
        /// Handles the global mouse click event and logs the information.
        /// </summary>
        private async void GlobalHook_MouseClick(object? sender, MouseEventArgs e)
        {
            // Create log entry
            var log = new MouseInputLog
            {
                Content = e,
                Timestamp = DateTime.Now
            };

            await _mouseInputStorageService.SaveLogAsync(log);
            
            _logger.LogDebug("Mouse clicked at {X}, {Y}", e.X, e.Y);
        }
    }
}