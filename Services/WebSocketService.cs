using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Linq;
using System.Collections.Generic;
using Fleck;
using Rhino;
using Rhino.Geometry;
using Newtonsoft.Json;
using Rhino.DocObjects;
using System.Threading.Tasks;
using Axys.Utilities;
using Rhino.Commands;

namespace Axys
{
    public class WebSocketService : IWebSocketService
    {
        private WebSocketServer server;
        private readonly List<IWebSocketConnection> allSockets = new();

        public void StartServer()
        {
            FleckLog.Level = LogLevel.Info;
            string port = "8765";
            string ip;
            try
            {
                ip = GetLocalIPAddressOfSelf();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting local IP address");
                return;
            }
            server = new WebSocketServer("ws://" + ip + ":" + port);
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    RhinoApp.WriteLine("Axys client connected.");
                    Logger.LogInfo("Axys client connected from " + socket.ConnectionInfo.ClientIpAddress);
                    allSockets.Add(socket);
                    BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "info", description: "Connection successful"));
                };
                socket.OnClose = () =>
                {
                    RhinoApp.WriteLine("Axys client disconnected.");
                    Logger.LogInfo("Axys client disconnected");
                    allSockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    Logger.LogDebug("Received from client: " + message);
                    dynamic data = JsonConvert.DeserializeObject(message);
                    string commandValue = data.command;
                    if (commandValue == "TrackObject")
                    {
                        try
                        {
                            RhinoApp.InvokeOnUiThread((Action)(() =>
                            {
                                var doc = RhinoDoc.ActiveDoc;
                                var result = CommandUtilities.ExecuteTrackObjectLogic(doc, this);
                                if (result == Result.Success)
                                {
                                    Logger.LogInfo("Object tracking information transmitted");
                                }
                                else
                                {
                                    Logger.LogError("Failed to track object");
                                    BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "error", description: "Failed to track object"));
                                }
                            }));
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Error processing TrackObject command: {ex.Message}");
                            BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "error", description: $"Error processing TrackObject command: {ex.Message}"));
                        }
                    }
                    if (commandValue == "ExportUSDZ")
                    {
                        var result = ExportToVision.Instance.ExportUSDZ(message);
                        if (result == Result.Success)
                        {
                            Logger.LogInfo("Exported USDZ successfully");
                        }
                        else
                        {
                            Logger.LogError("Failed to export USDZ");
                            BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "error", description: "Failed to export USDZ"));
                        }
                    }
                    else
                    {
                        ProcessUpdateMessage(message);
                    }
                };
            });
            RhinoApp.WriteLine("Axys started on ws://" + ip + ", awaiting connection...");
            Logger.LogInfo("WebSocket server started on ws://" + ip + ":" + port);
        }

        public void StopServer()
        {
            if (server == null)
                return;

            foreach (var socket in allSockets.ToList())
            {
                try { socket.Close(); } catch { }
            }
            server.Dispose();
            server = null;
            allSockets.Clear();
            Logger.LogInfo("WebSocket server stopped");
        }

        public bool IsServerRunning()
        {
            if (server == null)
                return false;
            return allSockets.Count > 0;
        }

        public void BroadcastMessage(string message)
        {
            foreach (var socket in allSockets.ToList())
            {
                if (socket.IsAvailable)
                {
                    Logger.LogInfo("Sending message: " + message);
                    socket.Send(message);
                }
                else
                {
                    Logger.LogWarning($"Socket {socket.ConnectionInfo.ClientIpAddress} is not available, removing from list.");
                    allSockets.Remove(socket);
                }
            }
        }

        private void ProcessUpdateMessage(string json)
        {
            RhinoObjectData updateMsg = JsonHandler.Deserialize(json);
            if (updateMsg.Type != "update")
                return;

            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                RhinoDoc doc = RhinoDoc.ActiveDoc;
                ObjectPositionManager positionManager = new ObjectPositionManager(doc);
                try
                {
                    string guidString = updateMsg.ObjectId.Replace("Object_", "");
                    if (Guid.TryParse(guidString, out Guid objectGuid))
                    {
                        Point3d newPosition = new Point3d(
                            updateMsg.Center.X * 1000,
                            updateMsg.Center.Y * 1000,
                            updateMsg.Center.Z * 1000
                        );
                        bool moveSuccess = positionManager.MoveObject(objectGuid, newPosition);

                        if (moveSuccess)
                        {
                            Logger.LogInfo($"Successfully moved object {objectGuid} to {newPosition}");
                            BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "info", description: $"Successfully moved object {objectGuid} to {newPosition}"));
                        }
                        else
                        {
                            Logger.LogError($"Failed to move object {objectGuid}");
                            BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "error", description: $"Failed to move object {objectGuid}"));
                        }
                    }
                    else
                    {
                        Logger.LogError($"Invalid object GUID in message: {guidString}");
                        BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "error", description: $"Invalid object GUID in message: {guidString}"));
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Error processing update message: {ex.Message}");
                    BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "error", description: $"Error processing update message: {ex.Message}"));
                }
            }));
        }

        private static string GetLocalIPAddressOfSelf()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => (n.OperationalStatus == OperationalStatus.Up) &&
                            (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                             n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));
            foreach (var networkInterface in networkInterfaces)
            {
                var ipProperties = networkInterface.GetIPProperties();
                var ipv4Addresses = ipProperties.UnicastAddresses
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ua => ua.Address)
                    .ToList();
                foreach (var address in ipv4Addresses)
                {
                    if (!IPAddress.IsLoopback(address) &&
                        !address.ToString().StartsWith("169.254.") &&
                        !address.IsIPv6LinkLocal)
                    {
                        return address.ToString();
                    }
                }
            }
            throw new Exception("No local IP address found on active network interfaces.");
        }

        public async Task BroadcastBinary(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Logger.LogError("Attempted to broadcast empty binary data.");
                BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "error", description: "Attempted to broadcast empty binary data."));
                return;
            }
            foreach (var socket in allSockets)
            {
                if (socket.IsAvailable)
                {
                    try
                    {
                        await socket.Send(data);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, $"Failed to send binary data: {ex.Message}");
                    }
                }
            }
        }
    }
}
