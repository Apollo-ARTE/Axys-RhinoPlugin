using System;
using System.Collections.Generic;
using Fleck;  
using Rhino;
using Rhino.Geometry;
using Newtonsoft.Json; 
using Rhino.DocObjects;

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
            server = new WebSocketServer("ws://10.20.62.154:8765");
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
                    ProcessUpdateMessage(message);
                };
            });
            RhinoApp.WriteLine("WebSocket server started on ws://<your-ip>:8765");
        }

        public static void BroadcastMessage(string message)
        {
            foreach (var socket in allSockets)
            {
                socket.Send(message);
            }
        }

        // public static void RegisterTrackedObject(string objectId);
        // {
        //     object obj = RhinoDoc.ActiveDoc.Objects.Find(objectId);
            
        // }

        public static void ProcessUpdateMessage(string json)
        {
            // Use the existing JsonHandler and RhinoObjectData.
            RhinoObjectData updateMsg = JsonHandler.Deserialize(json);
            if(updateMsg.Type != "update")
                return;
            
            // Ensure that document updates happen on the main thread.
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                // Extract the Guid from the objectId string.
                // Assuming the objectId is stored as "Sphere_{guid}"
                string guidString = updateMsg.ObjectId.Replace("Sphere_", "");
                if(Guid.TryParse(guidString, out Guid sphereGuid))
                {
                    RhinoObject obj = RhinoDoc.ActiveDoc.Objects.Find(sphereGuid);
                    if(obj != null)
                    {
                        // Retrieve the stored radius from the user string.
                        string radiusStr = obj.Attributes.GetUserString("Radius");
                        if (!double.TryParse(radiusStr, out double sphereRadius))
                        {
                            RhinoApp.WriteLine("Failed to retrieve the sphere's radius.");
                            return;
                        }
                        
                        // Create a new sphere using the updated center and the stored radius.
                        Point3d newCenter = new Point3d(updateMsg.Center.X, updateMsg.Center.Y, updateMsg.Center.Z);
                        Sphere newSphere = new Sphere(newCenter, sphereRadius);
                        
                        // Replace the existing sphere geometry.
                        // Note: Since Rhino stores the sphere as a Brep, convert it.
                        bool replaced = RhinoDoc.ActiveDoc.Objects.Replace(sphereGuid, newSphere.ToBrep());
                        if (replaced)
                        {
                            RhinoDoc.ActiveDoc.Views.Redraw();
                            RhinoApp.WriteLine("Sphere updated with new center: {0}", newCenter);
                        }
                        else
                        {
                            RhinoApp.WriteLine("Failed to replace the sphere geometry.");
                        }
                    }
                    else
                    {
                        RhinoApp.WriteLine("Sphere with ID {0} not found.", sphereGuid);
                    }
                }
                else
                {
                    RhinoApp.WriteLine("Invalid sphere GUID in message.");
                }
            }));
        }
    }
}
