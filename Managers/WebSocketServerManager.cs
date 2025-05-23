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
using RhinoPlugin.Utilities;
using Rhino.Commands;

namespace RhinoPlugin
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
            string ip = GetLocalIPAddressOfSelf();
            server = new WebSocketServer("ws://" + ip + ":" + port);
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    RhinoApp.WriteLine("WebSocket client connected.");
                    allSockets.Add(socket);
                    // Send a welcome message with a timestamp
                    socket.Send($"Server timestamp: {DateTimeOffset.Now.ToUnixTimeSeconds()} Selected object: Welcome");
                };
                socket.OnClose = () =>
                {
                    RhinoApp.WriteLine("WebSocket client disconnected.");
                    allSockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    RhinoApp.WriteLine("Received from client: " + message);
                    dynamic data = JsonConvert.DeserializeObject(message);
                    string commandValue = data.command;
                    if (commandValue == "TrackObject") 
                    {
                        RhinoApp.InvokeOnUiThread((Action)(() =>
                        {
                            var doc = RhinoDoc.ActiveDoc;
                            var result = CommandUtilities.ExecuteTrackObjectLogic(doc);
                            if (result == Result.Success)
                            {
                                RhinoApp.WriteLine("TrackObject logic executed successfully via WebSocket.");
                            }
                            else
                            {
                                RhinoApp.WriteLine("TrackObject logic failed via WebSocket.");
                            }
                        }));
                    } else {
                        ExportToVision.ExportUSDZ(message);
                        ProcessUpdateMessage(message);

                    }
                };
            });
            RhinoApp.WriteLine("WebSocket server started on ws://" + ip + ":" + port);
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
            RhinoApp.WriteLine("Sending message: " + message);
            foreach (var socket in allSockets)
            {
                socket.Send(message);
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
                    // Assuming the objectId is stored as "Sphere_{guid}"
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
                            RhinoApp.WriteLine($"Successfully moved object {objectGuid} to {newPosition}");

                            // // Optional: Broadcast confirmation back to clients
                            // BroadcastMessage(JsonHandler.Serialize(new {
                            //     Type = "move_confirmation",
                            //     ObjectId = updateMsg.ObjectId,
                            //     Status = "success"
                            // }));
                        }
                        else
                        {
                            // Log failed movement
                            RhinoApp.WriteLine($"Failed to move object {objectGuid}");

                            // // Optional: Broadcast error back to clients
                            // BroadcastMessage(JsonHandler.Serialize(new {
                            //     Type = "move_confirmation",
                            //     ObjectId = updateMsg.ObjectId,
                            //     Status = "failed"
                            // }));
                        }
                    }
                    else
                    {
                    RhinoApp.WriteLine($"Invalid object GUID in message: {guidString}");
                    }
                }
                catch (Exception ex)
                {
                    // Comprehensive error handling
                    RhinoApp.WriteLine($"Error processing update message: {ex.Message}");
                    
                    // // Optional: Broadcast error back to clients
                    // BroadcastMessage(JsonHandler.Serialize(new {
                    //     Type = "error",
                    //     Message = ex.Message
                    // }));
                }
            }));
        }
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
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
                RhinoApp.WriteLine("[WARN] Attempted to broadcast empty binary data.");
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
                        RhinoApp.WriteLine($"[ERROR] Failed to send binary data: {ex.Message}");
                    }
                }
            }
        }
    }
}   
