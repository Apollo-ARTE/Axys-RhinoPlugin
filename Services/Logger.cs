using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Configuration;
using Rhino;

public static class Logger
{
    private static readonly ILogger _logger;
    
    static Logger()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.IncludeScopes = false;
                    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
                })
        .SetMinimumLevel(LogLevel.Information);
});
        
        _logger = loggerFactory.CreateLogger("Axys");
    }
    
    public static void LogDebug(string message) => _logger.LogDebug(message);
    public static void LogInfo(string message) => _logger.LogInformation(message);
    public static void LogWarning(string message) => _logger.LogWarning(message);
    // Error messages are logged with both the logger and RhinoApp for visibility in the Rhino command line
    public static void LogError(string message)
    {
        _logger.LogError(message);
        RhinoApp.WriteLine($"ERROR: {message}");
    }
    public static void LogError(Exception ex, string message)
    {
        _logger.LogError(ex, message);
        RhinoApp.WriteLine($"ERROR: {message} - {ex.Message}");
    }
}