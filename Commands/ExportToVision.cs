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
using Axys.Managers.Geometry;
using Axys.Managers.Networking;
using Axys.Managers.ObjectHandling;

namespace Axys.Commands
{
    /// <summary>
    /// Exports geometry to USDZ format and streams the file to connected Axys
    /// clients via WebSocket.
    /// </summary>
    public class ExportToVision : Command
    {
        /// <summary>
        /// Identifier of the object last selected for export.
        /// </summary>
        public static Guid SelectedObjectId;
        /// <summary>
        /// File path of the most recently exported USDZ file.
        /// </summary>
        public static string LastExportedUSDZPath;

        /// <summary>
        /// Initializes a new instance of the command and stores it in
        /// <see cref="Instance"/> for access from script commands.
        /// </summary>
        public ExportToVision()
        {
            Instance = this;
        }

        /// <summary>
        /// Parses an incoming WebSocket message and selects the object to export.
        /// </summary>
        /// <param name="message">JSON message containing the export command.</param>
        /// <param name="doc">Active Rhino document.</param>
        /// <returns>The <see cref="RhinoObject"/> to export or <c>null</c> if not found.</returns>
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

        /// <summary>
        /// Singleton instance created by Rhino so other classes can invoke the command.
        /// </summary>
        public static ExportToVision Instance { get; private set; }
        public override string EnglishName => "ExportToVision";

        /// <summary>
        /// Prompts the user for an object selection and stores the identifier
        /// for export. If no object is selected the command attempts to create
        /// a pipe → mesh → block and selects the resulting instance.
        /// </summary>
        /// <param name="doc">Active Rhino document.</param>
        /// <param name="mode">Execution mode.</param>
        /// <returns><see cref="Result.Success"/> when an object is selected.</returns>
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

        /// <summary>
        /// Handles the <c>ExportUSDZ</c> command received from a WebSocket connection.
        /// </summary>
        /// <param name="message">JSON payload describing the export request.</param>
        /// <returns><see cref="Result.Success"/> if the request is dispatched.</returns>
        /// <remarks>Execution is marshalled onto the Rhino UI thread.</remarks>
        public static Result ExportUSDZ(dynamic message)
        {
            RhinoApp.InvokeOnUiThread(new Action(() => _ = HandleExecuteExportAsync(message)));
            return Result.Success;
        }

        /// <summary>
        /// Reads the last exported USDZ file from disk.
        /// </summary>
        /// <returns>Byte array of the USDZ file or <c>null</c> if the file cannot be read.</returns>
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

        /// <summary>
        /// Performs the export and network transmission of the selected object.
        /// </summary>
        /// <param name="message">Original message that triggered the export.</param>
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
