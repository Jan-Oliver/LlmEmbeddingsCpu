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
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    class Program
    {
        /// <summary>
        /// The main entry point of the application.
        /// Initializes logging, services, and starts the monitoring and processing tasks.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        [STAThread]
        static void Main(string[] args)
        {
            // Parse command line arguments
            bool processNow = args.Contains("--process-now", StringComparer.OrdinalIgnoreCase);

            // Configure Logging directory
            string basePath = AppContext.BaseDirectory;

            #if DEBUG
                Console.WriteLine("DEBUG mode");
                string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                LogLevel minLogLevel = LogLevel.Debug;
                Console.WriteLine("Log directory: " + logDirectory);
                Console.WriteLine("Min log level: " + minLogLevel);
            #else
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LlmEmbeddingsCpu", "logs");
                LogLevel minLogLevel = LogLevel.Warning;
            #endif

            Directory.CreateDirectory(logDirectory);
            string logFilePath = Path.Combine(logDirectory, "application-.log");

            // Configure the logger and only keep the last 5 days of logs
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(MapLogLevel(minLogLevel))
                .Enrich.FromLogContext()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5)
                .CreateLogger();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Log.CloseAndFlush();
            
            // Configure services
            var serviceProvider = ConfigureServices(logDirectory, minLogLevel);

            // Get the tracking services by concrete type
            var keyboardTracker = serviceProvider.GetRequiredService<KeyboardMonitorService>();
            var mouseTracker = serviceProvider.GetRequiredService<MouseMonitorService>();
            var windowTracker = serviceProvider.GetRequiredService<WindowMonitorrService>();

            // Get the scheduled service
            var scheduledProcessor = serviceProvider.GetRequiredService<ScheduledProcessingService>();

            // Subscribe to events if debug mode is enabled
            #if DEBUG
                if (processNow)
                {
                    Console.WriteLine("Running one-time processing...");
                    var processor = serviceProvider.GetRequiredService<ScheduledProcessingService>();
                    processor.ProcessNowAsync().Wait();
                    Console.WriteLine("Processing complete.");
                    return;
                }
            #endif
            
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

        /// <summary>
        /// Maps the Microsoft.Extensions.Logging.LogLevel to the Serilog.Events.LogEventLevel.
        /// </summary>
        /// <param name="level">The Microsoft.Extensions.Logging.LogLevel.</param>
        /// <returns>The corresponding Serilog.Events.LogEventLevel.</returns>
        private static Serilog.Events.LogEventLevel MapLogLevel(LogLevel level) => level switch
        {
            LogLevel.Trace => Serilog.Events.LogEventLevel.Verbose,
            LogLevel.Debug => Serilog.Events.LogEventLevel.Debug,
            LogLevel.Information => Serilog.Events.LogEventLevel.Information,
            LogLevel.Warning => Serilog.Events.LogEventLevel.Warning,
            LogLevel.Error => Serilog.Events.LogEventLevel.Error,
            LogLevel.Critical => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
        
        /// <summary>
        /// Configures the dependency injection container with all the necessary services.
        /// </summary>
        /// <param name="logDir">The directory to store log files.</param>
        /// <param name="minLogLevel">The minimum log level.</param>
        /// <returns>A configured <see cref="ServiceProvider"/>.</returns>
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