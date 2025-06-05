using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Axys
{
    public static class ExportHelpers
    {
        public struct ExportResult
        {
            public bool Success;
            public string Path;
            public Guid TemporaryCopyId;
        }

        public static ExportResult ExportSelectedObjectToUSDZ(RhinoDoc doc, GeometryBase preparedGeometry, Guid objectId)
        {
            Guid exportId = Guid.NewGuid();

            string tempDir = Path.Combine(Path.GetTempPath(), "RhinoExportTemp");
            Directory.CreateDirectory(tempDir);
            string fileName = $"Object_{objectId}.usdz";
            string path = Path.Combine(tempDir, fileName);
            Logger.LogDebug($"File will be saved as: {fileName}");
            Logger.LogDebug($"Exporting object {objectId} to temporary path: {path}");
            ExportToVision.LastExportedUSDZPath = path;

            Point3d origin;
            var rhinoObj = doc.Objects.Find(objectId);
            if (rhinoObj is InstanceObject)
            {
                origin = CalculateBlockInstanceOrigin(rhinoObj);
                Logger.LogDebug($"Origin from block instance center: {origin}");
            }
            else
            {
                var bbox = preparedGeometry.GetBoundingBox(true);
                double centerX = (bbox.Min.X + bbox.Max.X) / 2.0;
                double centerY = (bbox.Min.Y + bbox.Max.Y) / 2.0;
                double minZ = double.MaxValue;
                var vertices = new List<Point3d>();

                if (preparedGeometry is Brep brep)
                {
                    foreach (var v in brep.DuplicateVertices())
                        vertices.Add(v);
                }
                else if (preparedGeometry is Mesh mesh)
                {
                    vertices.AddRange(mesh.Vertices.ToPoint3dArray());
                }

                if (vertices.Count > 0)
                {
                    minZ = vertices.Min(v => v.Z);
                }

                origin = new Point3d(centerX, centerY, minZ);
                Logger.LogDebug($"Origin from geometry bounding box: {origin}");
            }

            string originString = $"{origin.X},{origin.Y},{origin.Z}";

            doc.Objects.UnselectAll();
            doc.Objects.Select(objectId);
            doc.Views.Redraw();

            string script = string.Format(
                "_-ExportWithOrigin {0} \"{1}\" _Enter _Enter",
                originString, path);

            Logger.LogDebug($"Export script command: {script}");
            RhinoApp.RunScript(script, false);

            bool fileExists = File.Exists(path);
            if (fileExists)
            {
                Logger.LogDebug($"File found after export: {path}");
            }
            else
            {
                Logger.LogWarning($"File NOT found after export: {path}");
            }

            return new ExportResult
            {
                Success = fileExists,
                Path = path,
                TemporaryCopyId = Guid.Empty
            };
        }

        public static Point3d CalculateBlockInstanceOrigin(RhinoObject obj)
        {
            if (!(obj is InstanceObject instanceObj))
            {
                Logger.LogWarning("Selected object is not an block.");
                return Point3d.Unset;
            }

            var instanceDef = instanceObj.InstanceDefinition;
            if (instanceDef == null)
            {
                Logger.LogWarning("Impossible to find the instance definition for the selected block.");
                return Point3d.Unset;
            }

            var geometryTrasformate = new List<GeometryBase>();
            foreach (var objId in instanceDef.GetObjectIds())
            {
                var rhinoObj = instanceObj.Document.Objects.Find(objId);
                if (rhinoObj == null) continue;

                var geo = rhinoObj.Geometry?.Duplicate();
                if (geo == null) continue;

                geo.Transform(instanceObj.InstanceXform);
                geometryTrasformate.Add(geo);
            }

            if (geometryTrasformate.Count == 0)
            {
                Logger.LogWarning("No geometry found in the block instance definition.");
                return Point3d.Unset;
            }

            BoundingBox bboxCombination = geometryTrasformate[0].GetBoundingBox(true);
            for (int i = 1; i < geometryTrasformate.Count; i++)
            {
                bboxCombination.Union(geometryTrasformate[i].GetBoundingBox(true));
            }

            return bboxCombination.Center;
        }
    }
}
