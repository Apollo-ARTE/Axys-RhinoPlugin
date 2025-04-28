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

        public override string EnglishName => "TrackObject";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Prompt user to select an object
            ObjRef[] objRef;
            Result rc = RhinoGet.GetMultipleObjects("Select object to track", false, ObjectType.AnyObject, out objRef);
            
            if (rc != Result.Success || objRef.Length == 0)
            {
                RhinoApp.WriteLine("No object was selected.");
                return Result.Cancel;
            }

            var positionManager = new ObjectPositionManager(doc);
            var objectDataArray = new List<RhinoObjectData>();

            for (int i = 0; i < objRef.Length; i++)
            {
                RhinoObject selectedObj = objRef[i].Object();
                if (selectedObj == null)
                {
                    RhinoApp.WriteLine("Failed to get the selected object.");
                    return Result.Failure;
                }

                // Create object position manager
                Guid objectId = selectedObj.Id;
                
                RhinoApp.WriteLine($"Selected object: {objectId}");
                
                // Get the object's current world position
                Point3d worldPosition = positionManager.GetAbsolutePosition(objectId);
                RhinoApp.WriteLine($"Start object position: {worldPosition}");

                // Prepare single object data
                RhinoObjectData objectData = positionManager.CreateObjectData(objectId);

                // Add object data to batch
                objectDataArray.Add(objectData);
            }

            RhinoObjectDataBatch objectDataBatch = positionManager.CreateObjectDataBatch(objectDataArray);

            string jsonMessage = JsonHandler.SerializeBatch(objectDataBatch);                
            // Check if WebSocket server is running before broadcasting
            if (WebSocketServerManager.IsServerRunning())
            {
                WebSocketServerManager.BroadcastMessage(jsonMessage);
                RhinoApp.WriteLine("Objects tracking information broadcasted.");
            }
            else
            {
                RhinoApp.WriteLine("Warning: WebSocket server connection is not established. Start the server first using WebSocketServerStart command and connect to it with the external device.");
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
    
            // Iterate through all objects in the document, does not display blocks
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
}

public class RhinoObjectInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string ObjectType { get; set; }
}
