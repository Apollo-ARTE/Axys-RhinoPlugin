using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using Newtonsoft.Json;

namespace RhinoPlugin
{
    // Command to start the WebSocket server
    public class WebSocketServerStartCommand : Command
    {
        public WebSocketServerStartCommand()
        {
            Instance = this;
        }

        public static WebSocketServerStartCommand Instance { get; private set; }
        public override string EnglishName => "WebSocketServerStart";
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                RhinoApp.WriteLine("Starting WebSocket server...");
                WebSocketServerManager.StartServer();
                RhinoApp.WriteLine("WebSocket server started successfully.");
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Failed to start WebSocket server: {ex.Message}");
                return Result.Failure;
            }
        }
    }

    public class TrackObjectCommand : Command
    {
        public static TrackObjectCommand Instance { get; private set; }

        // public TrackObjectCommand()
        // {
        //     Instance = this;
        // }

        public override string EnglishName => "TrackObject";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Prompt user to select an object
            ObjRef objRef = null;
            Result rc = RhinoGet.GetOneObject("Select object to track", false, ObjectType.AnyObject, out objRef);
            
            if (rc != Result.Success || objRef == null)
            {
                RhinoApp.WriteLine("No object was selected.");
                return Result.Cancel;
            }

            RhinoObject selectedObj = objRef.Object();
            if (selectedObj == null)
            {
                RhinoApp.WriteLine("Failed to get the selected object.");
                return Result.Failure;
            }

            // Create object position manager
            var positionManager = new ObjectPositionManager(doc);
            Guid objectId = selectedObj.Id;
            
            RhinoApp.WriteLine($"Selected object: {objectId}");
            
            // Get the object's current world position
            Point3d worldPosition = positionManager.GetAbsolutePosition(objectId);
            RhinoApp.WriteLine($"Start object position: {worldPosition}");

            // Prepare and broadcast object data
            RhinoObjectData objectData = positionManager.CreateObjectData(objectId);
            string jsonMessage = JsonHandler.Serialize(objectData);
            
            // Check if WebSocket server is running before broadcasting
            if (WebSocketServerManager.IsServerRunning())
            {
                WebSocketServerManager.BroadcastMessage(jsonMessage);
                RhinoApp.WriteLine("Object tracking information broadcasted.");
            }
            else
            {
                RhinoApp.WriteLine("Warning: WebSocket server is not running. Start the server first using StartWebSocketServer command.");
                return Result.Failure;
            }

            return Result.Success;
        }
    }

    public class GetObjectList: Command
    {
        public static GetObjectList Instance { get; private set; }

        public override string EnglishName => "GetObjectList";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var objects = new List<RhinoObjectInfo>();
    
            // Iterate through all objects in the document
            foreach (var rhinoObject in doc.Objects)
            {
                // Get the object's attributes which contain the name
                var attributes = rhinoObject.Attributes;
                
                // Create an info object for this Rhino object
                var info = new RhinoObjectInfo
                {
                    Id = rhinoObject.Id,
                    Name = attributes.Name,
                    ObjectType = rhinoObject.ObjectType.ToString()
                };
                
                RhinoApp.WriteLine($"Object ID: {info.Id}, Name: {info.Name}, Type: {info.ObjectType}");
                objects.Add(info);
            }
            
            // // Serialize to JSON
            // return JsonConvert.SerializeObject(objects, Formatting.Indented);
            return Result.Success;
        }
    }


//     public class RhinoPluginCommand : Command
//     {
//         // public RhinoPluginCommand()
//         // {
//         //     Instance = this;
//         // }

//         public static RhinoPluginCommand Instance { get; private set; }
//         public override string EnglishName => "RhinoPluginCommand";

//         protected override Result RunCommand(RhinoDoc doc, RunMode mode)
//         {
//             RhinoApp.WriteLine("Starting WebSocket server...");
//             WebSocketServerManager.StartServer();
//             RhinoApp.WriteLine("The {0} command will select an object to track now.", EnglishName);
            
//             // Create object position manager
//             var positionManager = new ObjectPositionManager(doc);

//             ObjRef objRef = null;
//             Result rc = RhinoGet.GetOneObject("Select object to track", false, ObjectType.AnyObject, out objRef);

//             if (rc != Result.Success || objRef == null)
//             {
//                 RhinoApp.WriteLine("No object was selected.");
//                 return rc;
//             }

//             RhinoObject selectedObj = objRef.Object();
//             if (selectedObj == null)
//             {
//                 RhinoApp.WriteLine("Failed to get the selected object.");
//                 return Result.Failure;
//             }

//             Guid objectId = selectedObj.Id;
//             RhinoApp.WriteLine("Selected object: {0}", objectId);
            
//             Point3d worldPosition = positionManager.GetAbsolutePosition(objectId);
//             RhinoApp.WriteLine($"Start object position: {worldPosition}");
//             // WebSocketServerManager.BroadcastMessage();
            
//             RhinoObjectData objectData = positionManager.CreateObjectData(objectId);

//             string jsonMessage = JsonHandler.Serialize(objectData);
//             WebSocketServerManager.BroadcastMessage(jsonMessage);

//             return Result.Success;
//         }
//     }
}

public class RhinoObjectInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string ObjectType { get; set; }
}
