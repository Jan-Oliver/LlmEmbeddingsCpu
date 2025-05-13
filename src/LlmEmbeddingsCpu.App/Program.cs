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
using Serilog;
using System.Windows.Forms;

namespace LlmEmbeddingsCpu.App
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Parse command line arguments
            bool processNow = args.Contains("--process-now", StringComparer.OrdinalIgnoreCase);
            bool isDebugMode = true; //args.Contains("--debug", StringComparer.OrdinalIgnoreCase);
            string logDir = "logs";

            // Configure Serilog for File Logging
            LogLevel minLogLevel = isDebugMode ? LogLevel.Debug : LogLevel.Warning;
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logDir);
            Directory.CreateDirectory(logDirectory);
            string logFilePath = Path.Combine(logDirectory, "application-.log");

            Log.Logger = new LoggerConfiguration()
                //.MinimumLevel.Is(minLogLevel) 
                .Enrich.FromLogContext()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Log.CloseAndFlush();
            
            // Configure services
            var serviceProvider = ConfigureServices(logDir, minLogLevel);

            if (processNow)
            {
                Log.Information("Running one-time processing...");
                var processor = serviceProvider.GetRequiredService<ScheduledProcessingService>();
                processor.ProcessNowAsync().Wait();
                Log.Information("Processing complete.");
                return;
            }

            // Get the tracking services by concrete type
            var keyboardTracker = serviceProvider.GetRequiredService<KeyboardMonitorService>();
            var mouseTracker = serviceProvider.GetRequiredService<MouseMonitorService>();
            var windowTracker = serviceProvider.GetRequiredService<WindowMonitorrService>();

            // Get the scheduled service
            var scheduledProcessor = serviceProvider.GetRequiredService<ScheduledProcessingService>();

            // Subscribe to events if debug mode is enabled
            if (isDebugMode){
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
            }
            
            // Start tracking
            keyboardTracker.StartTracking();
            mouseTracker.StartTracking();
            windowTracker.StartTracking();

            // Schedule daily processing
            scheduledProcessor.ScheduleProcessingAsync(TimeSpan.FromHours(0));
            
            Log.Information("Input tracking and scheduled processing started. Press Enter to exit.");
            
            // Use Application.Run() for the Windows Forms message loop
            Application.Run();
            
            // Stop tracking
            keyboardTracker.StopTracking();
            mouseTracker.StopTracking();
            windowTracker.StopTracking();
            scheduledProcessor.StopScheduledProcessingAsync().Wait();
        }
        
        private static ServiceProvider ConfigureServices(string logDir = "logs", LogLevel minLogLevel = LogLevel.Debug)
        {
            var services = new ServiceCollection();
            
            // Register logger
            services.AddLogging(configure =>
            {
                configure.ClearProviders();
                configure.AddSerilog();
                configure.SetMinimumLevel(minLogLevel);
            });
            
            // Register storage services
            services.AddSingleton<FileStorageService>(provider => new(logDir, provider.GetRequiredService<ILogger<FileStorageService>>()));
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