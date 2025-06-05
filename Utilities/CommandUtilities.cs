using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Axys.Managers.Networking;
using Axys.Managers.ObjectHandling;

namespace Axys.Utilities
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

            if (WebSocketServerManager.IsServerRunning())
            {
                WebSocketServerManager.BroadcastMessage(jsonMessage);
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
