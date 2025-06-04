using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Configuration;
using Rhino;

// Simple static logger without configuration file
public static class Logger
{
    private static readonly ILogger _logger;
    
    static Logger()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddConsole()
                .AddDebug()
                .SetMinimumLevel(LogLevel.Information));
        
        _logger = loggerFactory.CreateLogger("Application");
    }
    
    public static void LogInfo(string message) => _logger.LogInformation(message);
    
    public static void LogWarning(string message) => _logger.LogWarning(message);
    
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
    
    public static void LogDebug(string message) => _logger.LogDebug(message);
}