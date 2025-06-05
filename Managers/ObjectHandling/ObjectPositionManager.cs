using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects;

namespace Axys.Managers.ObjectHandling
{
    /// <summary>
    /// Provides helper methods for querying and modifying object positions within the Rhino document.
    /// </summary>
    public class ObjectPositionManager
    {
    private RhinoDoc _document;

        /// <summary>
        /// Initializes a new instance bound to the specified document.
        /// </summary>
        /// <param name="doc">Active Rhino document.</param>
        public ObjectPositionManager(RhinoDoc doc)
        {
            _document = doc ?? throw new ArgumentNullException(nameof(doc));
        }

    /// <summary>
    /// Calculates the absolute world position of an object.
    /// </summary>
    /// <param name="objectId">Object identifier.</param>
    /// <returns>World coordinate of the object center or <see cref="Point3d.Unset"/> if not found.</returns>
    public Point3d GetAbsolutePosition(Guid objectId)
    {
        RhinoObject selectedObj = _document.Objects.Find(objectId);
        if (selectedObj == null)
        {
            Logger.LogError("Failed to get the selected object.");
            return Point3d.Unset;
        }
        // Get the object's geometry (e.g., point, surface, or curve)
        GeometryBase geometry = selectedObj.Geometry;
        if (geometry == null)
        {
            Logger.LogError("Could not retrieve object geometry.");
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

    /// <summary>
    /// Moves an object to the given position in world coordinates.
    /// </summary>
    /// <param name="objectId">Identifier of the object to move.</param>
    /// <param name="newPosition">Target world position.</param>
    /// <returns>True if the move succeeded.</returns>
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
            Logger.LogError(ex, $"Error moving object: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Builds a <see cref="RhinoObjectData"/> structure describing a single object.
    /// </summary>
    /// <param name="objectId">Identifier of the object.</param>
    /// <returns>Serialized data for sending over WebSocket.</returns>
    public RhinoObjectData CreateObjectData(Guid objectId)
    {
        Point3d worldPosition = GetAbsolutePosition(objectId);
        string objectName = _document.Objects.Find(objectId)?.Name ?? "Unnamed";
        
        return new RhinoObjectData
        {
            Type = "create",
            ObjectName = objectName,
            ObjectId = $"Object_{objectId}",
            Center = new Center
            {
                X = worldPosition.X/1000,
                Y = worldPosition.Y/1000,
                Z = worldPosition.Z/1000
            },
            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// Packages multiple <see cref="RhinoObjectData"/> entries into a batch message.
    /// </summary>
    /// <param name="objectDataList">Objects to include in the batch.</param>
    /// <returns>Batch container object.</returns>
    public RhinoObjectDataBatch CreateObjectDataBatch(List<RhinoObjectData> objectDataList)
    {
        return new RhinoObjectDataBatch
        {
            Type = "batch_create",
            Objects = objectDataList,
            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        };
    }

    }
}
