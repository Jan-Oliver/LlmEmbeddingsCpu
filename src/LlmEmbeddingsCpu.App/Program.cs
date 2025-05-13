using System;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Data.FileStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using LlmEmbeddingsCpu.Data.MouseInputStorage;
using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Services.KeyboardMonitor;
using LlmEmbeddingsCpu.Services.MouseMonitor;
using LlmEmbeddingsCpu.Services.EmbeddingService;
using LlmEmbeddingsCpu.Services.BackgroundProcessing;
using LlmEmbeddingsCpu.Services.WindowMonitor;
using LlmEmbeddingsCpu.Data.WindowMonitorStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;

namespace LlmEmbeddingsCpu.App
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Parse command line arguments
            bool processNow = false;
            string logDir = "logs";
            
            // Configure services
            var serviceProvider = ConfigureServices(logDir);
            
            if (processNow)
            {
                // Run just the processing if --process-now flag is provided
                Console.WriteLine("Running one-time processing...");
                var processor = serviceProvider.GetRequiredService<ScheduledProcessingService>();
                processor.ProcessNowAsync().Wait();
                Console.WriteLine("Processing complete.");
                return;
            }
            
            // Get the tracking services by concrete type
            var keyboardTracker = serviceProvider.GetRequiredService<KeyboardMonitorService>();
            var mouseTracker = serviceProvider.GetRequiredService<MouseMonitorService>();
            var windowTracker = serviceProvider.GetRequiredService<WindowMonitorrService>();

            // Get the scheduled service
            var scheduledProcessor = serviceProvider.GetRequiredService<ScheduledProcessingService>();
            
            // Subscribe to events
            keyboardTracker.TextCaptured += (sender, text) =>
            {
                Console.WriteLine($"Keyboard captured: {text}");
            };
            
            mouseTracker.TextCaptured += (sender, text) =>
            {
                Console.WriteLine($"Mouse metrics: {text}");
            };

            windowTracker.ActiveWindowChanged += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("WINDOW CHANGED:");
                Console.WriteLine(e.WindowTitle);
                Console.WriteLine(e.ProcessName);
                Console.WriteLine(e.WindowHandle);
                Console.ResetColor();
            };
            
            // Start tracking
            keyboardTracker.StartTracking();
            mouseTracker.StartTracking();
            windowTracker.StartTracking();

            // Schedule daily processing at midnight
            // at 12:00am
            scheduledProcessor.ScheduleProcessingAsync(TimeSpan.FromHours(0));
            
            Console.WriteLine("Input tracking and scheduled processing started. Press Enter to exit.");
            
            // Use Application.Run() for the Windows Forms message loop
            Application.Run();
            //Console.ReadLine();
            
            // Stop tracking
            keyboardTracker.StopTracking();
            mouseTracker.StopTracking();
            windowTracker.StopTracking();
            scheduledProcessor.StopScheduledProcessingAsync().Wait();
        }
        
        private static ServiceProvider ConfigureServices(string logDir = "logs")
        {
            var services = new ServiceCollection();
            
            // Register logger
            services.AddLogging(configure => configure.AddConsole());
            
            // Register storage services
            services.AddSingleton<FileStorageService>(provider => new(logDir));
            services.AddSingleton<KeyboardInputStorageService>();
            services.AddSingleton<MouseInputStorageService>();
            services.AddSingleton<WindowMonitorStorageService>();
            services.AddSingleton<EmbeddingStorageService>();
            
            // Register embedding service
            services.AddSingleton<IEmbeddingService, IntfloatEmbeddingService>();
            
            // Register input tracking services by concrete type
            services.AddSingleton<KeyboardMonitorService>();
            services.AddSingleton<MouseMonitorService>();
            services.AddSingleton<WindowMonitorrService>();

            // Register scheduled processing service
            services.AddSingleton<ScheduledProcessingService>();
            
            return services.BuildServiceProvider();
        }
    }
}