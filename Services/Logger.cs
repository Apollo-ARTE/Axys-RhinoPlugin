using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace RhinoPlugin
{
    /// <summary>
    /// Static class to handle logging setup for the Rhino plugin
    /// </summary>
    public static class LoggingSetup
    {
        private static IServiceProvider _serviceProvider;
        private static ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initialize logging for the plugin. Call this once during plugin startup.
        /// </summary>
        public static void Initialize()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            _loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        }

        /// <summary>
        /// Get a logger for a specific type
        /// </summary>
        /// <typeparam name="T">The type to create logger for</typeparam>
        /// <returns>Configured logger instance</returns>
        public static ILogger<T> GetLogger<T>()
        {
            if (_loggerFactory == null)
            {
                Initialize();
            }
            return _loggerFactory.CreateLogger<T>();
        }

        /// <summary>
        /// Get the service provider for dependency injection
        /// </summary>
        public static IServiceProvider ServiceProvider => _serviceProvider;

        /// <summary>
        /// Clean up logging resources
        /// </summary>
        public static void Cleanup()
        {
            _loggerFactory?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Add logging services
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                
                // Add console logging (will show in Rhino's console)
                builder.AddConsole();
                
                // Add debug output (Visual Studio output window)
                builder.AddDebug();
                
                // Add file logging
                var logDirectory = GetLogDirectory();
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                var logFilePath = Path.Combine(logDirectory, "RhinoARPlugin.log");
                builder.AddFile(logFilePath, options =>
                {
                    options.MaxRollingFiles = 5;
                    options.FileSizeLimitBytes = 10 * 1024 * 1024; // 10MB
                });
                
                // Set minimum log levels
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
            });

            // Register your services
            services.AddTransient<GeometryManager>();
            // Add other services as needed
        }

        private static string GetLogDirectory()
        {
            // Create logs in user's AppData folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, "RhinoARPlugin", "Logs");
        }
    }

    /// <summary>
    /// Factory class for creating managers with proper logging injection
    /// </summary>
    public static class ManagerFactory
    {
        /// <summary>
        /// Create a GeometryManager with proper logging
        /// </summary>
        public static GeometryManager CreateGeometryManager()
        {
            var logger = LoggingSetup.GetLogger<GeometryManager>();
            return new GeometryManager(logger);
        }

        /// <summary>
        /// Get service from DI container
        /// </summary>
        public static T GetService<T>()
        {
            return LoggingSetup.ServiceProvider.GetRequiredService<T>();
        }
    }
}

// Extension method for file logging (you'll need to install Microsoft.Extensions.Logging.File NuGet package)
// Alternative: Use Serilog for more advanced file logging
namespace Microsoft.Extensions.Logging
{
    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath, Action<FileLoggerOptions> configure = null)
        {
            // This is a simplified version - you should use a proper file logging library
            // like Serilog or NLog for production use
            return builder.AddProvider(new SimpleFileLoggerProvider(filePath, configure));
        }
    }

    public class FileLoggerOptions
    {
        public int MaxRollingFiles { get; set; } = 5;
        public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024;
    }

    public class SimpleFileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly FileLoggerOptions _options;

        public SimpleFileLoggerProvider(string filePath, Action<FileLoggerOptions> configure)
        {
            _filePath = filePath;
            _options = new FileLoggerOptions();
            configure?.Invoke(_options);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _filePath);
        }

        public void Dispose() { }
    }

    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly object _lock = new object();

        public FileLogger(string categoryName, string filePath)
        {
            _categoryName = categoryName;
            _filePath = filePath;
        }

        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{_categoryName}] {message}";
            
            if (exception != null)
            {
                logEntry += Environment.NewLine + exception.ToString();
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Ignore file write errors to prevent logging from breaking the application
                }
            }
        }
    }
}