using System;
using Axys;
using Rhino;
using Rhino.Commands;
using Axys.Managers.Networking;

namespace Axys.Commands
{
/// <summary>
/// Command to start the WebSocket server so external Axys clients can connect
/// and receive data.
/// </summary>
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
