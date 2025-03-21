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
            
            Point3d worldPosition = GetObjectPosition.GetAbsolutePosition(objectId, doc);
            RhinoApp.WriteLine($"Start object position: {worldPosition}");

            // RhinoObjectData objectData = new RhinoObjectData
            // {
            //     Type = "create",
            //     ObjectId = $"Object_{objectId}",
            //     Position = new Position
            //     {
            //         X = position.X,
            //         Y = position.Y,
            //         Z = position.Z
            //     },
            //     Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            // };

            // Define translation vector
            Point3d newPosition;

            using (GetPoint getPoint = new GetPoint())
            {
                getPoint.SetCommandPrompt("Select the new position of the object");
                getPoint.Get();
                newPosition = getPoint.Point();
            }

            Rhino.Geometry.Vector3d translationVector = newPosition - worldPosition;

            // Apply the translation
            Rhino.Geometry.Transform moveTransform = Rhino.Geometry.Transform.Translation(translationVector);

            // Move the object
            doc.Objects.Transform(objectId, moveTransform, true);
            doc.Views.Redraw();

            // Check the new position
            worldPosition = GetObjectPosition.GetAbsolutePosition(objectId, doc);
            RhinoApp.WriteLine($"New object position: {worldPosition}");

            // string jsonMessage = JsonHandler.Serialize(objectData);
            // WebSocketServerManager.BroadcastMessage(jsonMessage);
            return Result.Success;
        }
    }
}

public class GetObjectPosition 
{
    public static Point3d GetAbsolutePosition(Guid objectId, RhinoDoc doc)
    {
        RhinoObject selectedObj = doc.Objects.Find(objectId);
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
}