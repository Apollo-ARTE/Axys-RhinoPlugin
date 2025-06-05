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
using System.Threading;
using System.Threading.Tasks;
using Axys.Utilities;
using Rhino.Commands;
using Axys.Commands;
using Axys.Managers.ObjectHandling;

namespace Axys.Managers.Networking
{
    public static class WebSocketServerManager
    {
        private static WebSocketServer server;
        private static List<IWebSocketConnection> allSockets = new List<IWebSocketConnection>();

        public static void StartServer()
        {
            FleckLog.Level = LogLevel.Info;
            // Bind the server to all network interfaces on port 8765.
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
                    // Send a welcome message with a timestamp
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
                                var result = CommandUtilities.ExecuteTrackObjectLogic(doc);
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
                        var result = ExportToVision.ExportUSDZ(message);
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
        public static bool IsServerRunning()
        {
            // Check if the server is not null and has been initialized
            if (server == null)
                return false;

            // Check if there are any active socket connections
            return allSockets.Count > 0;
        }
        public static void BroadcastMessage(string message)
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
                    allSockets.Remove(socket); // Clean up closed sockets
                }
            }
        }

        public static void ProcessUpdateMessage(string json)
        {
            // Use the existing JsonHandler and RhinoObjectData.
            RhinoObjectData updateMsg = JsonHandler.Deserialize(json);
            if(updateMsg.Type != "update")
                return;
            
            // Ensure that document updates happen on the main thread.
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                RhinoDoc doc = RhinoDoc.ActiveDoc;
                ObjectPositionManager positionManager = new ObjectPositionManager(doc);
                try
                {
                    // Extract the Guid from the objectId string.
                    string guidString = updateMsg.ObjectId.Replace("Object_", "");
                    if(Guid.TryParse(guidString, out Guid objectGuid))
                    {
                        // Prepare the new position from the update message
                        Point3d newPosition = new Point3d(
                            updateMsg.Center.X*1000, 
                            updateMsg.Center.Y*1000, 
                            updateMsg.Center.Z*1000
                        );
                        bool moveSuccess = positionManager.MoveObject(objectGuid, newPosition);

                        if (moveSuccess)
                        {
                            // Log successful movement
                            Logger.LogInfo($"Successfully moved object {objectGuid} to {newPosition}");
                            BroadcastMessage(MessageHandler.CreateAndSerializeMessage(type: "info", description: $"Successfully moved object {objectGuid} to {newPosition}"));
                        }
                        else
                        {
                            // Log failed movement
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
        
        public static string GetLocalIPAddressOfSelf()
        {
            // Get all network interfaces
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                // Filter for active ethernet or wireless interfaces
                .Where(n => (n.OperationalStatus == OperationalStatus.Up) &&
                            (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                            n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));
            foreach (var networkInterface in networkInterfaces)
            {
                // Get IP properties for the interface
                var ipProperties = networkInterface.GetIPProperties();
                // Find IPv4 addresses that are not loopback
                var ipv4Addresses = ipProperties.UnicastAddresses
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ua => ua.Address)
                    .ToList();
                foreach (var address in ipv4Addresses)
                {
                    // Skip loopback and link-local addresses
                    if (!IPAddress.IsLoopback(address) &&
                        !address.ToString().StartsWith("169.254.") && // Exclude APIPA addresses
                        !address.IsIPv6LinkLocal)
                    {
                        return address.ToString();
                    }
                }
            }
            throw new Exception("No local IP address found on active network interfaces.");
        }
        public static async Task BroadcastBinary(byte[] data)
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
