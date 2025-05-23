using System;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using RhinoPlugin;
using RhinoPlugin.Utilities;

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
            return CommandUtilities.ExecuteTrackObjectLogic(doc);
        }
    }

    public class ExportToVision : Command
    {
        public static Guid SelectedObjectId;
        public static string LastExportedUSDZPath;
        public ExportToVision()
        {
            Instance = this;
        }

        private static RhinoObject DeserializeAndSelectObject(dynamic message, RhinoDoc doc)
        {
            dynamic data = JsonConvert.DeserializeObject(message);
            string commandValue = data.command;
            RhinoApp.WriteLine("Deserialized command: " + commandValue);

            if (commandValue != "ExportUSDZ")
            {
                RhinoApp.WriteLine("Command is not ExportUSDZ.");
                return null;
            }
            // Prepare the object for export assignin id
            RhinoApp.RunScript("!_ExportToVision", false);
            SelectedObjectId = SelectionObjectManager.EnsureObjectIsSelected(doc, SelectedObjectId);
            if (SelectedObjectId == Guid.Empty) return null;

            RhinoObject selectedObj = doc.Objects.Find(SelectedObjectId);
            if (selectedObj != null)
            {
                string name = selectedObj.Name ?? "(unnamed)";
                string layer = doc.Layers[selectedObj.Attributes.LayerIndex].Name;
                RhinoApp.WriteLine($"[INFO] Selected object details: ID={SelectedObjectId}, Type={selectedObj.ObjectType}, Name={name}, Layer={layer}");
            }
            else
            {
                RhinoApp.WriteLine("Selected object not found in the document.");
                return null;
            }

            return selectedObj;
        }

        public static ExportToVision Instance { get; private set; }
        public override string EnglishName => "ExportToVision";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Prompt user to select an object to export
            Result selResult = RhinoGet.GetOneObject("Select object to export", false, ObjectType.AnyObject, out ObjRef objRef);
            if (selResult != Result.Success || objRef == null || objRef.Object() == null)
            {
                RhinoApp.WriteLine("No valid object selected.");
                return Result.Cancel;
            }

            SelectedObjectId = objRef.Object().Id;
            RhinoApp.WriteLine($"[DEBUG] Selected Object ID: {SelectedObjectId}");
            RhinoApp.WriteLine($"Tracking object with ID: {SelectedObjectId}");
            // // Serialize to JSON
            // return JsonConvert.SerializeObject(objects, Formatting.Indented);
            return Result.Success;
        }

        // Function called when an export command is received via WebSocket
        // ✅ CHECKPOINT — Stable export for polylines and block instances (Brep geometry).
        // Curve support still under development. This code path is considered reliable.
        public static void ExportUSDZ(dynamic message)
        {
            // Execute the entire export routine on the main UI thread to avoid autolayout issues
            RhinoApp.InvokeOnUiThread(new Action(() => _ = HandleExecuteExportAsync(message)));

        }
        static byte[] GetUSDZFileBytes()
        {
            if (string.IsNullOrEmpty(LastExportedUSDZPath) || !File.Exists(LastExportedUSDZPath))
            {
                RhinoApp.WriteLine("❌ No valid exported USDZ file found.");
                return null;
            }

            try
            {
                byte[] fileBytes = File.ReadAllBytes(LastExportedUSDZPath); // Read the USDZ file as bytes
                RhinoApp.WriteLine($"✅ USDZ file loaded successfully. Size: {fileBytes.Length} bytes"); // Log the success
                return fileBytes; // Return the byte array of the USDZ file
            }
            catch (Exception ex) // Catch any error that occurs while reading the file
            {
                RhinoApp.WriteLine($"❌ Error reading USDZ file: {ex.Message}"); // Log the error message
                return null; // Return null in case of an error
            }
        }

        private static async Task HandleExecuteExportAsync(dynamic message)
        {
            var doc = RhinoDoc.ActiveDoc;
            RhinoObject selectedObj = DeserializeAndSelectObject(message, doc);
            if (selectedObj == null) return;

            int materialIndex = 0;
            MaterialManager.ApplyMaterialIfMissing(selectedObj, doc, materialIndex);

            GeometryBase geometry = GeometryManager.PrepareGeometryForExport(doc, selectedObj);
            if (geometry == null)
            {
                RhinoApp.WriteLine($"Geometry preparation failed. Type: {selectedObj.Geometry?.GetType().Name ?? "null"}");
                return;
            }
            else
            {
                RhinoApp.WriteLine($"Geometry prepared successfully. Type: {geometry.GetType().Name}");
            }

            //IN TESTING: This is a temporary copy of the object to be exported
            var exportResult = GeometryManager.ExportSelectedObjectToUSDZ(doc, geometry, selectedObj.Id);
            if (!exportResult.Success)
            {
                RhinoApp.WriteLine("Export failed. No USDZ file generated.");
                return;
            }
            byte[] fileBytes = File.ReadAllBytes(exportResult.Path);
            await USDZExportManager.ExecuteExportAsync(fileBytes, exportResult.Path);

            if (exportResult.TemporaryCopyId != Guid.Empty)
            {
                doc.Objects.Delete(exportResult.TemporaryCopyId, true);
                RhinoApp.WriteLine("🧹 Temporary object deleted after export.");
            }
        }
    }
}

public class RhinoObjectInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string ObjectType { get; set; }
}

namespace RhinoPlugin.Utilities
{
    public static class CommandUtilities
    {
        public static Result ExecuteTrackObjectLogic(RhinoDoc doc)
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

                Guid objectId = selectedObj.Id;
                RhinoApp.WriteLine($"Selected object: {objectId}");

                Point3d worldPosition = positionManager.GetAbsolutePosition(objectId);
                RhinoApp.WriteLine($"Start object position: {worldPosition}");

                RhinoObjectData objectData = positionManager.CreateObjectData(objectId);
                objectDataArray.Add(objectData);
            }

            RhinoObjectDataBatch objectDataBatch = positionManager.CreateObjectDataBatch(objectDataArray);
            string jsonMessage = JsonHandler.SerializeBatch(objectDataBatch);

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
}