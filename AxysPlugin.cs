using System;
using Rhino.PlugIns;
using Microsoft.Extensions.Logging;

namespace Axys
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class AxysPlugin : Rhino.PlugIns.PlugIn
    {
        private static ILogger<AxysPlugin> _logger;
        public AxysPlugin()
        {
            Instance = this;
        }

        ///<summary>Gets the only instance of the AxysRhinoPlugin plug-in.</summary>
        public static AxysPlugin Instance { get; private set; }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                // Initialize logging first
                LoggingSetup.Initialize();
                _logger = LoggingSetup.GetLogger<AxysPlugin>();

                _logger.LogInformation("Plugin loading started");

                // Your other initialization code here

                _logger.LogInformation("Plugin loaded successfully");
                return LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to load plugin: {ex.Message}";
                _logger?.LogError(ex, "Plugin failed to load");
                return LoadReturnCode.ErrorShowDialog;
            }
        }
        
        /// <summary>
        /// Called when Rhino shuts down
        /// </summary>
        protected override void OnShutdown()
        {
            try
            {
                _logger?.LogInformation("Plugin shutting down");
                LoggingSetup.Cleanup();
            }
            catch (Exception ex)
            {
                // Log if possible, but don't throw during shutdown
                _logger?.LogError(ex, "Error during plugin shutdown");
            }
            finally
            {
                base.OnShutdown();
            }
        }
    }
}