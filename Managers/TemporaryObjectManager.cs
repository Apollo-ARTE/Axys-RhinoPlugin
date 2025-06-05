using System;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Axys
{
    public static class TemporaryObjectManager
    {
        public static Guid AddTemporaryGeometryToDoc(RhinoDoc doc, GeometryBase geometry)
        {
            if (geometry == null)
            {
                Logger.LogError("Failed to prepare geometry for export.");
                return Guid.Empty;
            }

            var bbox = geometry.GetBoundingBox(true);
            var baseCenter = new Point3d(
                (bbox.Min.X + bbox.Max.X) / 2.0,
                (bbox.Min.Y + bbox.Max.Y) / 2.0,
                bbox.Min.Z
            );
            var offset = new Vector3d(-baseCenter.X, -baseCenter.Y, -baseCenter.Z);
            var xformToOrigin = Transform.Translation(offset);

            geometry.Transform(xformToOrigin);
            Logger.LogDebug($"Geometry translated by {offset} to place its center at origin.");

            Guid copyId = Guid.Empty;
            if (geometry is Brep brep)
            {
                copyId = doc.Objects.AddBrep(brep);
            }
            else if (geometry is Mesh mesh)
            {
                copyId = doc.Objects.AddMesh(mesh);
            }

            if (copyId == Guid.Empty)
            {
                Logger.LogWarning("Temporary copy creation failed. Attempting export with original geometry.");
            }
            else
            {
                Logger.LogInfo("Temporary object added for export. Proceeding...");
            }
            return copyId;
        }

        public static void DeleteTemporaryGeometry(RhinoDoc doc, Guid copyId)
        {
            if (copyId != Guid.Empty)
            {
                var obj = doc.Objects.Find(copyId);
                if (obj != null)
                {
                    doc.Objects.Delete(obj, true);
                    Logger.LogDebug($"Temporary object {copyId} deleted.");
                }
                else
                {
                    Logger.LogWarning($"Temporary object {copyId} not found for deletion.");
                }
            }
            else
            {
                Logger.LogDebug("No temporary object to delete.");
            }
        }
    }
}
