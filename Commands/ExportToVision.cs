using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Axys;

namespace Axys.Commands
{
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
                WebSocketServerManager.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", "Selected object not found in the document."));
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
        public static Result ExportUSDZ(dynamic message)
        {
            // Execute the entire export routine on the main UI thread to avoid autolayout issues
            RhinoApp.InvokeOnUiThread(new Action(() => _ = HandleExecuteExportAsync(message)));
            return Result.Success;
        }

        static byte[] GetUSDZFileBytes()
        {
            if (string.IsNullOrEmpty(LastExportedUSDZPath) || !File.Exists(LastExportedUSDZPath))
            {
                Logger.LogError("No valid exported USDZ file found.");
                WebSocketServerManager.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", "No valid exported USDZ file found."));

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
                Logger.LogError($"Error reading USDZ file: {ex.Message}"); // Log the error message
                WebSocketServerManager.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", $"Error reading USDZ file: {ex.Message}"));

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

            GeometryBase geometry = GeometryConversion.PrepareGeometryForExport(doc, selectedObj);
            if (geometry == null)
            {
                Logger.LogError($"Geometry preparation failed. Type: {selectedObj.Geometry?.GetType().Name ?? "null"}");
                WebSocketServerManager.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", $"Geometry preparation failed. Type: {selectedObj.Geometry?.GetType().Name ?? "null"}"));
                return;
            }
            else
            {
                Logger.LogInfo($"Geometry prepared successfully. Type: {geometry.GetType().Name}");
            }

            // Export the selected object to USDZ
            var exportResult = ExportHelpers.ExportSelectedObjectToUSDZ(doc, geometry, selectedObj.Id);
            if (!exportResult.Success)
            {
                Logger.LogError("Export failed. No USDZ file generated.");
                WebSocketServerManager.BroadcastMessage(MessageHandler.CreateAndSerializeMessage("error", "Export failed. No USDZ file generated."));
                return;
            }
            byte[] fileBytes = File.ReadAllBytes(exportResult.Path);
            await USDZExportManager.ExecuteExportAsync(fileBytes, exportResult.Path);

        }
    }
}
