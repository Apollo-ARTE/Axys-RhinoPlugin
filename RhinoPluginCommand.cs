using System;
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
    public class StartWebSocketServerCommand : Command
    {

        public StartWebSocketServerCommand()
        {
            Instance = this;
        }

        public static StartWebSocketServerCommand Instance { get; private set; }
        public override string EnglishName => "StartWebSocketServer";
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

        public TrackObjectCommand()
        {
            Instance = this;
        }

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


    public class RhinoPluginCommand : Command
    {
        public RhinoPluginCommand()
        {
            Instance = this;
        }

        public static RhinoPluginCommand Instance { get; private set; }
        public override string EnglishName => "RhinoPluginCommand";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Starting WebSocket server...");
            WebSocketServerManager.StartServer();
            RhinoApp.WriteLine("The {0} command will select an object to track now.", EnglishName);
            
            // Create object position manager
            var positionManager = new ObjectPositionManager(doc);

            ObjRef objRef = null;
            Result rc = RhinoGet.GetOneObject("Select object to track", false, ObjectType.AnyObject, out objRef);

            if (rc != Result.Success || objRef == null)
            {
                RhinoApp.WriteLine("No object was selected.");
                return rc;
            }

            RhinoObject selectedObj = objRef.Object();
            if (selectedObj == null)
            {
                RhinoApp.WriteLine("Failed to get the selected object.");
                return Result.Failure;
            }

            Guid objectId = selectedObj.Id;
            RhinoApp.WriteLine("Selected object: {0}", objectId);
            
            Point3d worldPosition = positionManager.GetAbsolutePosition(objectId);
            RhinoApp.WriteLine($"Start object position: {worldPosition}");
            // WebSocketServerManager.BroadcastMessage();
            
            RhinoObjectData objectData = positionManager.CreateObjectData(objectId);

            string jsonMessage = JsonHandler.Serialize(objectData);
            WebSocketServerManager.BroadcastMessage(jsonMessage);

            return Result.Success;
        }
    }
}

public class ObjectPositionManager 
{

    private RhinoDoc _document;

        public ObjectPositionManager(RhinoDoc doc)
        {
            _document = doc ?? throw new ArgumentNullException(nameof(doc));
        }
    public Point3d GetAbsolutePosition(Guid objectId)
    {
        RhinoObject selectedObj = _document.Objects.Find(objectId);
        if (selectedObj == null)
        {
            RhinoApp.WriteLine("Failed to get the selected object.");
            return Point3d.Unset;
        }
        // Get the object's geometry (e.g., point, surface, or curve)
        GeometryBase geometry = selectedObj.Geometry;
        if (geometry == null)
        {
            RhinoApp.WriteLine("Could not retrieve object geometry.");
            return Point3d.Unset;
        }

        // If the object is a point, return its world position
        if (geometry is Point point)
        {
            return point.Location;
        }

        // If the object is not a point, try to get its bounding box center
        BoundingBox bbox = geometry.GetBoundingBox(true);
        Point3d worldPosition = bbox.Center;

        return worldPosition;

    }

    public bool MoveObject(Guid objectId, Point3d newPosition)
    {
        Point3d currentPosition = GetAbsolutePosition(objectId);

        if (currentPosition == Point3d.Unset)
        {
            return false;
        }

        Rhino.Geometry.Vector3d translationVector = newPosition - currentPosition;
        // Apply the translation
        Rhino.Geometry.Transform moveTransform = Rhino.Geometry.Transform.Translation(translationVector);
        try
        {
            _document.Objects.Transform(objectId, moveTransform, true);
            _document.Views.Redraw();
            return true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"Error moving object: {ex.Message}");
            return false;
        }
    }

    public RhinoObjectData CreateObjectData(Guid objectId)
    {
        Point3d worldPosition = GetAbsolutePosition(objectId);
        
        return new RhinoObjectData
        {
            Type = "create",
            ObjectId = $"Object_{objectId}",
            Center = new Center
            {
                X = worldPosition.X,
                Y = worldPosition.Y,
                Z = worldPosition.Z
            },
            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        };
    }

}