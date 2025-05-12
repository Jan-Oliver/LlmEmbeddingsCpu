using Gma.System.MouseKeyHook;
using System;
using System.Windows.Forms;
using System.Timers;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.MouseInputStorage;

namespace LlmEmbeddingsCpu.Services.MouseMonitor
{
    public class MouseMonitorService(MouseInputStorageService mouseInputStorageService)
    {
        private IMouseEvents? _globalHook;
        private readonly MouseInputStorageService _mouseInputStorageService = mouseInputStorageService;
        public event EventHandler<string>? TextCaptured;

        public void StartTracking()
        {
            // Subscribe to global mouse events
            _globalHook = Hook.GlobalEvents();
            _globalHook.MouseClick += GlobalHook_MouseClick;
                        
            Console.WriteLine("Mouse tracking started...");
        }
        
        public void StopTracking()
        {
            if (_globalHook != null)
            {
                _globalHook.MouseClick -= GlobalHook_MouseClick;
            }
            
            Console.WriteLine("Mouse tracking stopped.");
        }

        private async void GlobalHook_MouseClick(object? sender, MouseEventArgs e)
        {
            // Create log entry
            var log = new MouseInputLog
            {
                Content = e,
                Timestamp = DateTime.Now
            };

            await _mouseInputStorageService.SaveLogAsync(log);
            
            // Log the event
            Console.WriteLine($"Mouse clicked at {e.X}, {e.Y}");
        }
    }
}