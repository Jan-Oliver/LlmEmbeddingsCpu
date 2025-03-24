using Gma.System.MouseKeyHook;
using System;
using System.Windows.Forms;
using System.Timers;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Core.Enums;

namespace LlmEmbeddingsCpu.Services.InputTracking
{
    public class MouseMonitorService : IInputTrackingService
    {
        private IMouseEvents? _globalHook;
        private readonly IInputLogRepository _repository;
        private int _clickCount = 0;
        private System.Timers.Timer _timer;
        private const int TIMER_INTERVAL_MS = 60000; // 1 minute

        public event EventHandler<string>? TextCaptured;

        public MouseMonitorService(IInputLogRepository repository)
        {
            _repository = repository;
            
            // Setup timer to log metrics every minute
            _timer = new System.Timers.Timer(TIMER_INTERVAL_MS);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
        }

        public void StartTracking()
        {
            // Subscribe to global mouse events
            _globalHook = Hook.GlobalEvents();
            _globalHook.MouseClick += GlobalHook_MouseClick;
            
            // Start the timer
            _timer.Start();
            
            Console.WriteLine("Mouse tracking started...");
        }
        
        public void StopTracking()
        {
            // Log final metrics
            LogClickFrequency();
            
            // Stop the timer
            _timer.Stop();
            
            if (_globalHook != null)
            {
                _globalHook.MouseClick -= GlobalHook_MouseClick;
            }
            
            Console.WriteLine("Mouse tracking stopped.");
        }

        private void GlobalHook_MouseClick(object? sender, MouseEventArgs e)
        {
            // Increment click counter
            _clickCount++;
        }
        
        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            LogClickFrequency();
        }
        
        private void LogClickFrequency()
        {
            // Calculate clicks per minute
            double clicksPerMinute = _clickCount / (TIMER_INTERVAL_MS / 60000.0);
            
            // Create log entry
            var log = new InputLog
            {
                Content = $"ClicksPerMinute={clicksPerMinute:F2}",
                Type = InputType.Mouse,
                Timestamp = DateTime.Now
            };
            
            // Save asynchronously
            _ = _repository.SaveLogAsync(log);
            
            // Raise event
            TextCaptured?.Invoke(this, log.Content);
            
            // Reset counter
            _clickCount = 0;
        }
    }
}