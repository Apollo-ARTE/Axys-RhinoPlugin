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
            RhinoApp.WriteLine("The {0} command will add a sphere now.", EnglishName);

            // Prompt for sphere center
            Point3d center;
            using (GetPoint getCenter = new GetPoint())
            {
                getCenter.SetCommandPrompt("Select the center of the sphere");
                if (getCenter.Get() != GetResult.Point)
                {
                    RhinoApp.WriteLine("No center point was selected.");
                    return getCenter.CommandResult();
                }
                center = getCenter.Point();
            }

            double radius = 0;
            Result rc = RhinoGet.GetNumber("Enter the radius of the sphere", false, ref radius);
            if (rc != Result.Success)
            {
                RhinoApp.WriteLine("Invalid radius input.");
                return rc;
            }

            Sphere sphere = new Sphere(center, radius);
            Guid sphereId = doc.Objects.AddSphere(sphere);
            if (sphereId == Guid.Empty)
            {
                RhinoApp.WriteLine("Failed to add sphere to document.");
                return Result.Failure;
            }
            doc.Views.Redraw();
            RhinoApp.WriteLine("Sphere added with ID: {0}", sphereId);

            // Attach metadata to the object
            RhinoObject obj = doc.Objects.Find(sphereId);
            if (obj != null)
            {
                obj.Attributes.SetUserString("Radius", radius.ToString());
                obj.CommitChanges();
            }

            RhinoObjectData sphereData = new RhinoObjectData
            {
                Type = "create",
                ObjectId = $"Sphere_{sphereId}",
                Center = new Center
                {
                    X = center.X,
                    Y = center.Y,
                    Z = center.Z
                },
                Radius = radius,
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };
            string jsonMessage = JsonHandler.Serialize(sphereData);
            WebSocketServerManager.BroadcastMessage(jsonMessage);
            return Result.Success;
        }
    }

    
}