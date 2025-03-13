using System;
using System.Collections.Generic;
using Fleck;  
using Rhino;

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
            server = new WebSocketServer("ws://10.20.58.109:8765");
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
                    // Here you could parse commands sent from the iPad.
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
    }
}
