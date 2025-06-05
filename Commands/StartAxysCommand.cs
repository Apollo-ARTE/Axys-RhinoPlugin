using System;
using Axys;
using Rhino;
using Rhino.Commands;
using Axys.Managers.Networking;

namespace Axys.Commands
{
    /// <summary>
    /// Rhino command that starts the local WebSocket server allowing external
    /// Axys clients to connect and exchange messages.
    /// </summary>
    public class StartAxysCommand : Command
    {
        /// <summary>
        /// Singleton instance created by Rhino so the command can be executed programmatically.
        /// </summary>
        public static StartAxysCommand Instance { get; private set; }
        public override string EnglishName => "Axys";

        /// <summary>
        /// Starts the WebSocket server on the local machine.
        /// </summary>
        /// <param name="doc">Active Rhino document.</param>
        /// <param name="mode">Command run mode.</param>
        /// <returns>Success if the server was started.</returns>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                Logger.LogInfo("Starting Axys...");
                WebSocketServerManager.StartServer();
                return Result.Success;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start Axys: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}
