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
using Axys;
using Axys.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Axys
{
    // Command to start the WebSocket server that allows axys to connect and receive data.
    public class StartAxysCommand : Command
    {
        private readonly IWebSocketService _webSocketService;
        public static StartAxysCommand Instance { get; private set; }

        public StartAxysCommand()
        {
            Instance = this;
            _webSocketService = AxysPlugin.Instance.Services.GetService<IWebSocketService>();
        }

        public override string EnglishName => "Axys";
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                Logger.LogInfo("Starting Axys...");
                _webSocketService.StartServer();
                return Result.Success;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start Axys: {ex.Message}");
                return Result.Failure;
            }
        }
    }

    public class TrackObjectCommand : Command
    {
        private readonly IWebSocketService _webSocketService;
        public static TrackObjectCommand Instance { get; private set; }

        public TrackObjectCommand()
        {
            Instance = this;
            _webSocketService = AxysPlugin.Instance.Services.GetService<IWebSocketService>();
        }

        public override string EnglishName => "TrackObject";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return CommandUtilities.ExecuteTrackObjectLogic(doc, _webSocketService);
        }
    }

    public class ExportToVision : Command
    {
        public static Guid SelectedObjectId;
        public static string LastExportedUSDZPath;
        private readonly IWebSocketService _webSocketService;

        public ExportToVision()
        {
            Instance = this;
            _webSocketService = AxysPlugin.Instance.Services.GetService<IWebSocketService>();
        }

        private RhinoObject DeserializeAndSelectObject(dynamic message, RhinoDoc doc)
        {
            dynamic data = JsonConvert.DeserializeObject(message);
            string commandValue = data.command;
            Logger.LogInfo("Deserialized command: " + commandValue);

            if (commandValue != "ExportUSDZ")
            {
                Logger.LogWarning("Command is not ExportUSDZ.");
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
                Logger.LogInfo($"Selected object details: ID={SelectedObjectId}, Type={selectedObj.ObjectType}, Name={name}, Layer={layer}");
            }
            else
            {
                Logger.LogError("Selected object not found in the document.");
                _webSocketService.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", "Selected object not found in the document."));
                return null;
            }

            return selectedObj;
        }

        public static ExportToVision Instance { get; private set; }
        public override string EnglishName => "ExportToVision";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Result selResult = RhinoGet.GetOneObject("Select object to export", false, ObjectType.AnyObject, out ObjRef objRef);
            if (selResult != Result.Success || objRef == null || objRef.Object() == null)
            {
                Logger.LogInfo("No valid object selected. Launching ScriptPipeMeshBlock…");
                RhinoApp.RunScript("_ScriptPipeMeshBlock", false);

                var recentBlock = doc.Objects.GetObjectList(ObjectType.InstanceReference)
                                            .OrderByDescending(o => o.RuntimeSerialNumber)
                                            .FirstOrDefault();

                if (recentBlock == null)
                {
                    Logger.LogError("Fallback failed: no block instance found.");
                    return Result.Cancel;
                }

                SelectedObjectId = recentBlock.Id;
                Logger.LogDebug($" Fallback OK. Selected block ID: {SelectedObjectId}");
                return Result.Success;
            }

            SelectedObjectId = objRef.Object().Id;
            Logger.LogDebug($"Tracking object with ID: {SelectedObjectId}");
            return Result.Success;
        }

        // Function called when an export command is received via WebSocket
        // Curve support still under development. This code path is considered reliable.
        public Result ExportUSDZ(dynamic message)
        {
            RhinoApp.InvokeOnUiThread(new Action(() => _ = HandleExecuteExportAsync(message)));
            return Result.Success;
        }

        private byte[] GetUSDZFileBytes()
        {
            if (string.IsNullOrEmpty(LastExportedUSDZPath) || !File.Exists(LastExportedUSDZPath))
            {
                Logger.LogError("No valid exported USDZ file found.");
                _webSocketService.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", "No valid exported USDZ file found."));

                return null;
            }

            try
            {
                byte[] fileBytes = File.ReadAllBytes(LastExportedUSDZPath); // Read the USDZ file as bytes
                Logger.LogDebug($"USDZ file loaded successfully. Size: {fileBytes.Length} bytes"); // Log the success
                return fileBytes; // Return the byte array of the USDZ file
            }
            catch (Exception ex) // Catch any error that occurs while reading the file
            {
                Logger.LogError($"Error reading USDZ file: {ex.Message}");
                _webSocketService.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", $"Error reading USDZ file: {ex.Message}"));

                return null; // Return null in case of an error
            }
        }

        private async Task HandleExecuteExportAsync(dynamic message)
        {
            var doc = RhinoDoc.ActiveDoc;
            RhinoObject selectedObj = DeserializeAndSelectObject(message, doc);
            if (selectedObj == null) return;

            int materialIndex = 0;
            MaterialManager.ApplyMaterialIfMissing(selectedObj, doc, materialIndex);

            GeometryBase geometry = GeometryConversion.PrepareGeometryForExport(doc, selectedObj);
            if (geometry == null)
            {
                Logger.LogError($"Geometry preparation failed. Type: {selectedObj.Geometry?.GetType().Name ?? "null"}");
                _webSocketService.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", $"Geometry preparation failed. Type: {selectedObj.Geometry?.GetType().Name ?? "null"}"));
                return;
            }
            else
            {
                Logger.LogInfo($"Geometry prepared successfully. Type: {geometry.GetType().Name}");
            }

            //IN TESTING: This is a temporary copy of the object to be exported
            var exportResult = ExportHelpers.ExportSelectedObjectToUSDZ(doc, geometry, selectedObj.Id);
            if (!exportResult.Success)
            {
                Logger.LogError("Export failed. No USDZ file generated.");
                _webSocketService.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", "Export failed. No USDZ file generated."));
                return;
            }
            byte[] fileBytes = File.ReadAllBytes(exportResult.Path);
            await USDZExportManager.ExecuteExportAsync(_webSocketService, fileBytes, exportResult.Path);

            if (exportResult.TemporaryCopyId != Guid.Empty)
            {
                doc.Objects.Delete(exportResult.TemporaryCopyId, true);
                Logger.LogDebug("Temporary object deleted after export.");
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

namespace Axys.Utilities
{
    public static class CommandUtilities
    {
        public static Result ExecuteTrackObjectLogic(RhinoDoc doc, IWebSocketService webSocketService)
        {
            // Prompt user to select an object
            ObjRef[] objRef;
            Result rc = RhinoGet.GetMultipleObjects("Select object to track", false, ObjectType.AnyObject, out objRef);

            if (rc != Result.Success || objRef.Length == 0)
            {
                Logger.LogError("No object was selected.");
                return Result.Cancel;
            }

            var positionManager = new ObjectPositionManager(doc);
            var objectDataArray = new List<RhinoObjectData>();

            for (int i = 0; i < objRef.Length; i++)
            {
                RhinoObject selectedObj = objRef[i].Object();
                if (selectedObj == null)
                {
                    Logger.LogError("Failed to get the selected object.");
                    return Result.Failure;
                }

                Guid objectId = selectedObj.Id;
                Logger.LogInfo($"Selected object: {objectId}");

                Point3d worldPosition = positionManager.GetAbsolutePosition(objectId);
                Logger.LogDebug($"Start object position: {worldPosition}");

                RhinoObjectData objectData = positionManager.CreateObjectData(objectId);
                objectDataArray.Add(objectData);
            }

            RhinoObjectDataBatch objectDataBatch = positionManager.CreateObjectDataBatch(objectDataArray);
            string jsonMessage = JsonHandler.SerializeBatch(objectDataBatch);

            if (webSocketService.IsServerRunning())
            {
                webSocketService.BroadcastMessage(jsonMessage);
                Logger.LogInfo("Objects tracking information broadcasted.");
            }
            else
            {
                Logger.LogError("Warning: WebSocket server connection is not established. Start the server first using Axys command and connect to it with the external device.");
                return Result.Failure;
            }

            return Result.Success;
        }
    }
}