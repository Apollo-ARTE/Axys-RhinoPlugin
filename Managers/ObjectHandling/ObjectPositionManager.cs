using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects;

namespace Axys.Managers.ObjectHandling
{
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
