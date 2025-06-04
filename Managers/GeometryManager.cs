using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Axys;
using System;
using System.Collections.Generic;
using System.IO;


namespace Axys
{
    public class GeometryManager
    {

        // Converts a PolyCurve into a joined Brep pipe. Each segment is piped and joined together.
        // polyCurve - The PolyCurve to convert.
        // doc - The active Rhino document, used for tolerance settings.
        // pipeRadius - The radius to use for pipe creation.
        // returns - A joined Brep representing the piped curve, or null if conversion failed.
        public static Brep ConvertPolyCurveToBrep(PolyCurve polyCurve, RhinoDoc doc, double pipeRadius)
        {
            var breps = new List<Brep>();

            foreach (Curve subCurve in polyCurve.Explode())
            {
                Brep[] pipeBreps = Brep.CreatePipe(
                    subCurve,
                    pipeRadius,
                    false, // localBlending
                    PipeCapMode.Round,
                    false, // fitRail
                    doc.ModelAbsoluteTolerance,
                    doc.ModelAngleToleranceRadians
                );

                if (pipeBreps != null && pipeBreps.Length > 0)
                {
                    breps.AddRange(pipeBreps);
                    Logger.LogDebug($"Created {pipeBreps.Length} pipe breps from subcurve.");

                }
                else
                {
                    Logger.LogWarning($"Failed to create pipe for subcurve: {subCurve.GetType().Name}");
                }
            }

            if (breps.Count == 0)
            {
                Logger.LogWarning("No breps were created from subcurves.");
                return null;
            }

            var joined = Brep.JoinBreps(breps, doc.ModelAbsoluteTolerance);
            if (joined == null || joined.Length == 0)
            {
                Logger.LogWarning("JoinBreps failed: no joined breps returned.");
                return null;
            }
            else
            {
                Logger.LogDebug($"Successfully joined {joined.Length} breps into a single brep.");
            }
            return joined[0];
        }

        // Converts a list of Curve objects into a single joined Brep pipe.
        // curves - The list of curves to pipe.
        // doc - The active Rhino document.
        // pipeRadius - The radius of the pipe to create.
        // return - joined Brep from all pipes, or null if the conversion fails.
        public static Brep ConvertMultipleCurvesToJoinedPipe(List<Curve> curves, RhinoDoc doc, double pipeRadius)
        {
            var breps = new List<Brep>();

            foreach (var curve in curves)
            {
                Brep[] pipeBreps = Brep.CreatePipe(
                    curve,
                    pipeRadius,
                    false, // localBlending
                    PipeCapMode.Round,
                    false, // fitRail
                    doc.ModelAbsoluteTolerance,
                    doc.ModelAngleToleranceRadians
                );

                if (pipeBreps != null && pipeBreps.Length > 0)
                {
                    breps.AddRange(pipeBreps);
                }
                else
                {
                    Logger.LogWarning("⚠️ Failed to create pipe for one of the curves.");
                }
            }

            if (breps.Count == 0)
                return null;

            var joined = Brep.JoinBreps(breps, doc.ModelAbsoluteTolerance);
            return joined != null && joined.Length > 0 ? joined[0] : null;
        }

        public static GeometryBase PrepareGeometryForExport(RhinoDoc doc, RhinoObject selectedObj)
        {

            GeometryBase geometry = selectedObj.Geometry?.Duplicate();
            if (geometry == null)
            {
                Logger.LogError("Geometry duplication failed. Type: " + (selectedObj.Geometry?.GetType().Name ?? "null"));
                return null;
            }
            Logger.LogDebug("Geometry duplicated successfully. Type: " + geometry.GetType().Name);

            // Handle direct curve or polycurve exports first
            if (geometry is Curve || geometry is PolyCurve)
            {
                Logger.LogError($"Converting selected curve type {geometry.GetType().Name} to mesh via pipe.");
                // Build a list of curves
                var curvesList = new List<Curve>();
                if (geometry is PolyCurve pc)
                    curvesList.AddRange(pc.Explode().OfType<Curve>());
                else
                    curvesList.Add((Curve)geometry);
                // Create joint pipe Brep and mesh it
                double pipeRadius = 0.1;
                var brep = ConvertMultipleCurvesToJoinedPipe(curvesList, doc, pipeRadius);
                if (brep == null)
                {
                    Logger.LogError("Failed to generate pipe geometry from curves.");
                    return null;
                }
                var meshes = Mesh.CreateFromBrep(brep, MeshingParameters.Default);
                if (meshes == null || meshes.Length == 0)
                {
                    Logger.LogError("Failed to mesh the pipe brep from curves.");
                    return null;
                }
                return meshes[0];
            }

            // Handle direct extrusion selection by converting to Brep
            if (geometry is Extrusion directExt)
            {
                Logger.LogDebug("Converting direct Extrusion to Brep.");
                var directBrep = directExt.ToBrep();
                if (directBrep == null)
                {
                    Logger.LogError("Failed to convert direct Extrusion to Brep.");
                    return null;
                }
                return directBrep;
            }

            if (geometry is InstanceReferenceGeometry instanceRef)
            {
                var instanceDef = SelectionObjectManager.FindInstanceDefinitionByGuid(doc, instanceRef.ParentIdefId);
                if (instanceDef == null)
                {
                    Logger.LogError("Failed to retrieve instance definition.");
                    return null;
                }

                var curves = new List<Curve>();
                var breps = new List<Brep>();
                var meshes = new List<Mesh>();

                Logger.LogDebug("Contents of instance definition:");
                foreach (var objId in instanceDef.GetObjectIds())
                {
                    var obj = doc.Objects.Find(objId);
                    if (obj == null) continue;
                    var geo = obj.Geometry;
                    Logger.LogDebug($"  - Type: {geo.GetType().Name}");

                    if (geo is Curve c)
                        curves.Add(c);
                    else if (geo is PolyCurve pc)
                        curves.Add(pc);
                    else if (geo is Brep b)
                        breps.Add(b);
                    else if (geo is Mesh m)
                        meshes.Add(m);
                    else if (geo is Extrusion ext)
                    {
                        Logger.LogDebug("Converting block Extrusion to Brep.");
                        var extrusionBrep = ext.ToBrep();
                        if (extrusionBrep != null)
                            breps.Add(extrusionBrep);
                        else
                            Logger.LogError("Failed to convert Extrusion to Brep.");
                    }
                }

                if (curves.Count > 0)
                {
                    Logger.LogDebug($"{curves.Count} curve(s) found in block definition. Proceeding with pipe generation.");
                    double pipeRadius = 0.1;
                    Brep joinedPipe = ConvertMultipleCurvesToJoinedPipe(curves, doc, pipeRadius);
                    if (joinedPipe == null)
                    {
                        Logger.LogError("Failed to generate pipe geometry from block curves.");
                        return null;
                    }
                    geometry = joinedPipe;
                }
                else if (breps.Count > 0)
                {
                    geometry = breps[0].DuplicateBrep();
                }
                else if (meshes.Count > 0)
                {
                    geometry = meshes[0].DuplicateMesh();
                }
                else
                {
                    Logger.LogDebug($"Curve count: {curves.Count}, Brep count: {breps.Count}, Mesh count: {meshes.Count}");
                    Logger.LogDebug("No supported geometry found in block definition.");
                    return null;
                }
            }
            else if (!(geometry is Brep) && !(geometry is Mesh))
            {
                Logger.LogDebug($"Geometry type before conversion: {geometry.GetType().Name}");
                if (geometry is PolyCurve polyCurve)
                {
                    double pipeRadius = 0.1;
                    Brep joinedPipe = ConvertPolyCurveToBrep(polyCurve, doc, pipeRadius);
                    if (joinedPipe == null)
                    {
                        Logger.LogError("Failed to convert polycurve to pipe brep.");
                        return null;
                    }
                    var pipeMeshes2 = Mesh.CreateFromBrep(joinedPipe, MeshingParameters.Default);
                    if (pipeMeshes2 == null || pipeMeshes2.Length == 0)
                    {
                        Logger.LogError("Failed to mesh the pipe brep.");
                        return null;
                    }
                    return pipeMeshes2[0];
                }
                else if (geometry is Curve singleCurve)
                {
                    double pipeRadius = 0.1;
                    Brep[] pipe = Brep.CreatePipe(
                        singleCurve, pipeRadius, false, PipeCapMode.Round, false,
                        doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians);
                    if (pipe == null || pipe.Length == 0)
                    {
                        Logger.LogError("Failed to convert single curve to pipe.");
                        return null;
                    }
                    var pipeMeshes3 = Mesh.CreateFromBrep(pipe[0], MeshingParameters.Default);
                    if (pipeMeshes3 == null || pipeMeshes3.Length == 0)
                    {
                        Logger.LogError("Failed to mesh the single pipe brep.");
                        return null;
                    }
                    return pipeMeshes3[0];
                }
                else
                {
                    Logger.LogError("Selected geometry is not supported for export.");
                    return null;
                }
            }

            return geometry;
        }

        public static Guid AddTemporaryGeometryToDoc(RhinoDoc doc, GeometryBase geometry)
        {
            if (geometry == null)
            {
                Logger.LogError("Failed to prepare geometry for export.");
                return Guid.Empty;
            }

            // 1. Compute the geometry's bounding box in its current world-space.
            var bbox = geometry.GetBoundingBox(true);
            // 2. Compute translation that moves the BOX CENTER to the origin
            var baseCenter = new Point3d(
                (bbox.Min.X + bbox.Max.X) / 2.0,
                (bbox.Min.Y + bbox.Max.Y) / 2.0,
                bbox.Min.Z
            );
            var offset = new Vector3d(-baseCenter.X, -baseCenter.Y, -baseCenter.Z);
            var xformToOrigin = Transform.Translation(offset);

            // 3. Apply translation
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
        //Needs testing, it's not clear if this is the right way to delete the temporary object
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



        public struct ExportResult
        {
            public bool Success;
            public string Path;
            public Guid TemporaryCopyId;
        }
        public static ExportResult ExportSelectedObjectToUSDZ(RhinoDoc doc, GeometryBase preparedGeometry, Guid objectId)
        {
            Guid exportId = Guid.NewGuid();

            // Save the exported file in a dedicated temp directory
            string tempDir = Path.Combine(Path.GetTempPath(), "RhinoExportTemp");
            Directory.CreateDirectory(tempDir);
            string fileName = $"Object_{objectId}.usdz";
            string path = Path.Combine(tempDir, fileName);
            Logger.LogDebug($"File will be saved as: {fileName}");
            Logger.LogDebug($"Exporting object {objectId} to temporary path: {path}");
            ExportToVision.LastExportedUSDZPath = path;

            // Origin calculation block
            Point3d origin;
            var rhinoObj = doc.Objects.Find(objectId);
            if (rhinoObj is InstanceObject)
            {
                origin = CalculateBlockInstanceOrigin
    (rhinoObj);
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

        public class ScriptPipeMeshBlockCommand : Command
        {
            public static ScriptPipeMeshBlockCommand Instance { get; private set; }

            public override string EnglishName => "ScriptPipeMeshBlock";

            protected override Result RunCommand(RhinoDoc doc, RunMode mode)
            {
                // Select curves
                ObjRef[] objRefs;
                Result rc = RhinoGet.GetMultipleObjects(
                    "Select curves to pipe, mesh, and block",
                    false,
                    ObjectType.Curve,
                    out objRefs);

                if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
                {
                    Logger.LogWarning("No valid curves selected.");
                    return Result.Cancel;
                }

                // Clear previous selections
                doc.Objects.UnselectAll();

                // Select the chosen curves
                foreach (var o in objRefs)
                    doc.Objects.Select(o.ObjectId);

                doc.Views.Redraw();

                // Pipe each curve
                RhinoApp.RunScript("-_Pipe 0.3 0.3 _Enter _Enter", false);

                // Mesh
                RhinoApp.RunScript("-_Mesh _Enter _Enter", false);

                // Retrieve the last created mesh objects
                int expectedMeshes = objRefs.Length;
                var meshObjs = doc.Objects.GetObjectList(ObjectType.Mesh)
                    .OrderByDescending(o => o.RuntimeSerialNumber)
                    .Take(expectedMeshes)
                    .ToList();

                if (meshObjs.Count == 0)
                {
                    Logger.LogError("No meshes found for block creation.");
                    return Result.Failure;
                }

                // Select the created meshes
                doc.Objects.UnselectAll();
                foreach (var m in meshObjs)
                    doc.Objects.Select(m.Id);

                // Create a block from the selected meshes
                var basePt = new Point3d(0, 0, 0);
                string idList = string.Join(" ", meshObjs.Select(m => m.Id));
                RhinoApp.RunScript($"-_Block {idList} {basePt.X},{basePt.Y},{basePt.Z} _Enter", false);

                Logger.LogDebug("Pipe → Mesh → Block pipeline completed.");

                return Result.Success;
            }
        }
    }
}