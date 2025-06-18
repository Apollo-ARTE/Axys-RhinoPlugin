using System;
using Rhino.PlugIns;
using Microsoft.Extensions.Logging;

namespace Axys
{
    /// <summary>
    /// Entry point for the Axys Rhino plug-in. Rhino creates a single instance
    /// of this class which manages the plug-in life cycle.  The plug-in logs its
    /// load and shutdown events using <see cref="Logger"/>.
    /// </summary>
    public class AxysPlugin : Rhino.PlugIns.PlugIn
    {
        public override string PluginName => "Axys";
        public AxysPlugin()
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the singleton instance of the plug-in created by Rhino.
        /// </summary>
        public static AxysPlugin Instance { get; private set; }

        /// <summary>
        /// Initializes the plug-in when Rhino loads it.
        /// </summary>
        /// <param name="errorMessage">Receives a description in case the plug-in fails to load.</param>
        /// <returns>The <see cref="LoadReturnCode"/> describing load success.</returns>
        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                Logger.LogInfo("Plugin loaded successfully");
                return LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to load plugin: {ex.Message}";
                Logger.LogError(ex, "Plugin failed to load");
                return LoadReturnCode.ErrorShowDialog;
            }
        }
        
        /// <summary>
        /// Invoked by Rhino during application shutdown to perform cleanup.
        /// </summary>
        protected override void OnShutdown()
        {
            try
            {
                Logger.LogInfo("Plugin shutting down");
            }
            catch (Exception ex)
            {
                // Log if possible, but don't throw during shutdown
                Logger.LogError(ex, "Error during plugin shutdown");
            }
            finally
            {
                base.OnShutdown();
            }
        }
    }
}