using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Data.FileSystemIO;
using LlmEmbeddingsCpu.Data.KeyboardLogIO;
using LlmEmbeddingsCpu.Data.MouseLogIO;
using LlmEmbeddingsCpu.Data.EmbeddingIO;
using LlmEmbeddingsCpu.Data.ProcessingStateIO;
using LlmEmbeddingsCpu.Data.WindowLogIO;
using LlmEmbeddingsCpu.Services.KeyboardMonitor;
using LlmEmbeddingsCpu.Services.MouseMonitor;
using LlmEmbeddingsCpu.Services.EmbeddingService;
using LlmEmbeddingsCpu.Services.WindowMonitor;
using LlmEmbeddingsCpu.Services.ResourceMonitor;
using LlmEmbeddingsCpu.Services.ContinuousProcessing;
using LlmEmbeddingsCpu.Services.CronProcessing;
using LlmEmbeddingsCpu.Services.Aggregation;
using LlmEmbeddingsCpu.Common.Enums;
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
        /// Initializes logging, services, and starts the appropriate process based on launch mode.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        [STAThread]
        static async Task Main(string[] args)
        {
            // Parse launch mode from command line arguments
            var launchMode = ParseLaunchMode(args);
            
            // Configure Logging directory
            string basePath = AppContext.BaseDirectory;

            #if DEBUG
                Console.WriteLine("DEBUG mode");
                string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                LogLevel minLogLevel = LogLevel.Debug;
                Console.WriteLine("Log directory: " + logDirectory);
                Console.WriteLine("Min log level: " + minLogLevel);
                Console.WriteLine("Launch mode: " + launchMode);
            #else
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LlmEmbeddingsCpu", "logs");
                LogLevel minLogLevel = LogLevel.Information;
            #endif

            Directory.CreateDirectory(logDirectory);
            string logFilePath = Path.Combine(logDirectory, $"application-{launchMode.ToString().ToLower()}-.log");

            // Configure the logger and only keep the last 5 days of logs
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(MapLogLevel(minLogLevel))
                .Enrich.FromLogContext()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5)
                .CreateLogger();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Log.CloseAndFlush();
            
            // Configure services based on launch mode
            var serviceProvider = ConfigureServices(logDirectory, minLogLevel, launchMode);

            // Execute the appropriate process
            if (launchMode == LaunchMode.Logger)
            {
                await RunLoggerMode(serviceProvider, args);
            }
            else if (launchMode == LaunchMode.Processor)
            {
                await RunProcessorMode(serviceProvider);
            }
            else if (launchMode == LaunchMode.CronProcessor)
            {
                await RunCronProcessorMode(serviceProvider);
            }
            else if (launchMode == LaunchMode.Aggregator)
            {
                await RunAggregatorMode(serviceProvider);
            }
        }

        /// <summary>
        /// Parses the launch mode from command line arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>The parsed launch mode, defaults to Logger if no valid mode is found.</returns>
        private static LaunchMode ParseLaunchMode(string[] args)
        {
            if (args.Contains("--processor", StringComparer.OrdinalIgnoreCase))
                return LaunchMode.Processor;
            if (args.Contains("--cron-processor", StringComparer.OrdinalIgnoreCase))
                return LaunchMode.CronProcessor;
            if (args.Contains("--aggregator", StringComparer.OrdinalIgnoreCase))
                return LaunchMode.Aggregator;
            
            return LaunchMode.Logger; // Default mode
        }

        /// <summary>
        /// Runs the logger mode with all monitoring services.
        /// </summary>
        private static async Task RunLoggerMode(ServiceProvider serviceProvider, string[] args)
        {
            var keyboardTracker = serviceProvider.GetRequiredService<KeyboardMonitorService>();
            var mouseTracker = serviceProvider.GetRequiredService<MouseMonitorService>();
            var windowTracker = serviceProvider.GetRequiredService<WindowMonitorrService>();
            var resourceMonitor = serviceProvider.GetRequiredService<ResourceMonitorService>();
            
            keyboardTracker.StartTracking();
            mouseTracker.StartTracking();
            windowTracker.StartTracking();
            resourceMonitor.StartMonitoring();
                        
            Application.Run();
            
            keyboardTracker.StopTracking();
            mouseTracker.StopTracking();
            windowTracker.StopTracking();
            resourceMonitor.StopMonitoring();
        }

        /// <summary>
        /// Runs the processor mode for continuous processing.
        /// </summary>
        private static async Task RunProcessorMode(ServiceProvider serviceProvider)
        {
            var continuousProcessor = serviceProvider.GetRequiredService<ContinuousProcessingService>();
            Log.Information("Starting continuous processor mode");
            await continuousProcessor.StartProcessingAsync();
            Log.Information("Continuous processor mode completed");
        }

        /// <summary>
        /// Runs the cron processor mode for scheduled processing.
        /// </summary>
        private static async Task RunCronProcessorMode(ServiceProvider serviceProvider)
        {
            var cronProcessor = serviceProvider.GetRequiredService<CronProcessingService>();
            Log.Information("Starting cron processor mode");
            await cronProcessor.StartProcessingAsync();
            Log.Information("Cron processor mode completed");
        }

        /// <summary>
        /// Runs the aggregator mode for housekeeping.
        /// </summary>
        private static async Task RunAggregatorMode(ServiceProvider serviceProvider)
        {
            var aggregator = serviceProvider.GetRequiredService<AggregationService>();
            Log.Information("Starting aggregator mode");
            await aggregator.StartAggregationAsync();
            Log.Information("Aggregator mode completed");
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
        /// Configures the dependency injection container with services based on launch mode.
        /// </summary>
        /// <param name="logDir">The directory to store log files.</param>
        /// <param name="minLogLevel">The minimum log level.</param>
        /// <param name="launchMode">The launch mode determining which services to register.</param>
        /// <returns>A configured <see cref="ServiceProvider"/>.</returns>
        private static ServiceProvider ConfigureServices(string logDir = "logs", LogLevel minLogLevel = LogLevel.Debug, LaunchMode launchMode = LaunchMode.Logger)
        {
            var services = new ServiceCollection();
            
            // Register logger
            services.AddLogging(configure =>
            {
                configure.ClearProviders();
                configure.AddSerilog();
                configure.SetMinimumLevel(minLogLevel);
            });
            
            // Register common storage services (needed by all modes)
            services.AddSingleton<FileSystemIOService>(provider => new(logDir, provider.GetRequiredService<ILogger<FileSystemIOService>>()));
            services.AddSingleton<EmbeddingIOService>();
            services.AddSingleton<ProcessingStateIOService>();

            // Register services based on launch mode
            switch (launchMode)
            {
                case LaunchMode.Logger:
                    RegisterLoggerServices(services);
                    break;
                case LaunchMode.Processor:
                    RegisterProcessorServices(services);
                    break;
                case LaunchMode.CronProcessor:
                    RegisterCronProcessorServices(services);
                    break;
                case LaunchMode.Aggregator:
                    RegisterAggregatorServices(services);
                    break;
            }
            
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Registers services required for the logger mode.
        /// </summary>
        private static void RegisterLoggerServices(ServiceCollection services)
        {
            // Register all storage services
            services.AddSingleton<KeyboardLogIOService>();
            services.AddSingleton<MouseLogIOService>();
            services.AddSingleton<WindowLogIOService>();
            
            // Register embedding service
            services.AddSingleton<IEmbeddingService, IntfloatEmbeddingService>();
            
            // Register input tracking services by concrete type
            services.AddSingleton<KeyboardMonitorService>();
            services.AddSingleton<MouseMonitorService>();
            services.AddSingleton<WindowMonitorrService>();

            // Register resource monitoring service
            services.AddSingleton<ResourceMonitorService>();
        }

        /// <summary>
        /// Registers services required for the processor mode.
        /// </summary>
        private static void RegisterProcessorServices(ServiceCollection services)
        {
            // Register keyboard input storage service
            services.AddSingleton<KeyboardLogIOService>();
            
            // Register embedding service
            services.AddSingleton<IEmbeddingService, IntfloatEmbeddingService>();
            
            // Register continuous processing service
            services.AddSingleton<ContinuousProcessingService>();
        }

        /// <summary>
        /// Registers services required for the cron processor mode.
        /// </summary>
        private static void RegisterCronProcessorServices(ServiceCollection services)
        {
            // Register keyboard input storage service
            services.AddSingleton<KeyboardLogIOService>();
            
            // Register embedding service
            services.AddSingleton<IEmbeddingService, IntfloatEmbeddingService>();
            
            // Register cron processing service
            services.AddSingleton<CronProcessingService>();
        }

        /// <summary>
        /// Registers services required for the aggregator mode.
        /// </summary>
        private static void RegisterAggregatorServices(ServiceCollection services)
        {
            // Register all storage services
            services.AddSingleton<KeyboardLogIOService>();
            services.AddSingleton<MouseLogIOService>();
            services.AddSingleton<WindowLogIOService>();
            
            // Register aggregation service
            services.AddSingleton<AggregationService>();
        }
    }
}