using System;
using Axys;
using Rhino;
using Rhino.Commands;

namespace Axys.Commands
{
    // Command to start the WebSocket server that allows axys to connect and receive data.
    public class StartAxysCommand : Command
    {
        public static StartAxysCommand Instance { get; private set; }
        public override string EnglishName => "Axys";

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
