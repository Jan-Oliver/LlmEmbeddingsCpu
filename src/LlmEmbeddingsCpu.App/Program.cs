using System;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Data.FileStorage;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using LlmEmbeddingsCpu.Data.MouseInputStorage;
using LlmEmbeddingsCpu.Data.EmbeddingStorage;
using LlmEmbeddingsCpu.Services.InputTracking;
using LlmEmbeddingsCpu.Services.EmbeddingService;
using LlmEmbeddingsCpu.Services.BackgroundProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;
using System.ComponentModel.Design;

namespace LlmEmbeddingsCpu.App
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Parse command line arguments
            bool processNow = true;
            string logDir = "logs";
            
            // Configure services
            var serviceProvider = ConfigureServices(logDir);
            
            if (processNow)
            {
                // Run just the processing if --process-now flag is provided
                Console.WriteLine("Running one-time processing...");
                var processor = serviceProvider.GetRequiredService<IScheduledProcessingService>();
                processor.ProcessNowAsync().Wait();
                Console.WriteLine("Processing complete.");
                return;
            }
            
            // Get the tracking services by concrete type
            var keyboardTracker = serviceProvider.GetRequiredService<KeyboardMonitorService>();
            var mouseTracker = serviceProvider.GetRequiredService<MouseMonitorService>();
            
            // Get the scheduled service
            var scheduledProcessor = serviceProvider.GetRequiredService<IScheduledProcessingService>();
            
            // Subscribe to events
            keyboardTracker.TextCaptured += (sender, text) =>
            {
                Console.WriteLine($"Keyboard captured: {text}");
            };
            
            mouseTracker.TextCaptured += (sender, text) =>
            {
                Console.WriteLine($"Mouse metrics: {text}");
            };
            
            // Start tracking
            keyboardTracker.StartTracking();
            mouseTracker.StartTracking();
            
            // Schedule daily processing at midnight
            scheduledProcessor.ScheduleProcessingAsync(new TimeSpan(0, 0, 0)).Wait();
            
            Console.WriteLine("Input tracking and scheduled processing started. Press Enter to exit.");
            
            // Use Application.Run() for the Windows Forms message loop
            Application.Run();
            
            // Stop tracking
            keyboardTracker.StopTracking();
            mouseTracker.StopTracking();
            scheduledProcessor.StopScheduledProcessingAsync().Wait();
        }
        
        private static ServiceProvider ConfigureServices(string logDir = "logs")
        {
            var services = new ServiceCollection();
            
            // Register logger
            services.AddLogging(configure => configure.AddConsole());
            
            // Register storage services
            services.AddSingleton<FileStorageService>(provider => new FileStorageService(logDir));
            services.AddSingleton<KeyboardInputStorageService>();
            services.AddSingleton<MouseInputStorageService>();
            services.AddSingleton<EmbeddingStorageService>();
            
            // Register embedding service
            services.AddSingleton<IEmbeddingService, EmbeddingService>();
            
            // Register scheduled processing service
            services.AddSingleton<IScheduledProcessingService, ScheduledProcessingService>();
            
            // Register input tracking services by concrete type
            services.AddSingleton<KeyboardMonitorService>();
            services.AddSingleton<MouseMonitorService>();
            
            return services.BuildServiceProvider();
        }
    }
}