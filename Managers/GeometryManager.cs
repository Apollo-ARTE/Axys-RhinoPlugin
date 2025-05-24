using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using RhinoPlugin;
using System;
using System.Collections.Generic;
using System.IO;


namespace RhinoPlugin
{
    public static class GeometryManager
    {
        // New method: Attempts to get a joined Brep from user selection.
        public static Brep GetJoinedBrepFromSelection(RhinoDoc doc)
        {
            ObjRef[] objRefs;
            Result rc = RhinoGet.GetMultipleObjects("Select objects to display in VisionPRO", false, ObjectType.Surface | ObjectType.PolysrfFilter, out objRefs);

            if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
            {
                RhinoApp.WriteLine("No objects were selected.");
                return null;
            }

            var breps = objRefs.Select(o => o.Brep()).Where(b => b != null).ToList();

            if (breps.Count == 1)
            {
                RhinoApp.WriteLine("Single brep selected.");
                return breps[0];
            }
            else if (breps.Count > 1)
            {
                double tol = doc.ModelAbsoluteTolerance;
                var joined = Brep.JoinBreps(breps, tol);
                if (joined != null && joined.Length > 0)
                {
                    RhinoApp.WriteLine($"Successfully joined into {joined.Length} brep(s).");
                    return joined[0];
                }
                else
                {
                    RhinoApp.WriteLine("Failed to join breps.");
                    return null;
                }
            }

            return null;
        }



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
                }
                else
                {
                    RhinoApp.WriteLine($"Created {pipeBreps.Length} pipe breps from subcurve.");
                }
            }

            if (breps.Count == 0)
            {
                RhinoApp.WriteLine("No breps were created from subcurves.");
                return null;
            }

            var joined = Brep.JoinBreps(breps, doc.ModelAbsoluteTolerance);
            if (joined == null || joined.Length == 0)
            {
                RhinoApp.WriteLine("JoinBreps failed: no joined breps returned.");
                return null;
            }
            else
            {
                RhinoApp.WriteLine($"Successfully joined {joined.Length} breps into a single brep.");
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
                    RhinoApp.WriteLine("⚠️ Failed to create pipe for one of the curves.");
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
                RhinoApp.WriteLine("Geometry duplication failed. Type: " + (selectedObj.Geometry?.GetType().Name ?? "null"));
                return null;
            }
            RhinoApp.WriteLine("Geometry duplicated successfully. Type: " + geometry.GetType().Name);

            // Handle direct curve or polycurve exports first
            if (geometry is Curve || geometry is PolyCurve)
            {
                RhinoApp.WriteLine($"[DEBUG] Converting selected curve type {geometry.GetType().Name} to mesh via pipe.");
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
                    RhinoApp.WriteLine("Failed to generate pipe geometry from curves.");
                    return null;
                }
                var meshes = Mesh.CreateFromBrep(brep, MeshingParameters.Default);
                if (meshes == null || meshes.Length == 0)
                {
                    RhinoApp.WriteLine("❌ Failed to mesh the pipe brep from curves.");
                    return null;
                }
                return meshes[0];
            }

            // Handle direct extrusion selection by converting to Brep
            if (geometry is Extrusion directExt)
            {
                RhinoApp.WriteLine("[DEBUG] Converting direct Extrusion to Brep.");
                var directBrep = directExt.ToBrep();
                if (directBrep == null)
                {
                    RhinoApp.WriteLine("⚠️ Failed to convert direct Extrusion to Brep.");
                    return null;
                }
                return directBrep;
            }

            if (geometry is InstanceReferenceGeometry instanceRef)
            {
                var instanceDef = SelectionObjectManager.FindInstanceDefinitionByGuid(doc, instanceRef.ParentIdefId);
                if (instanceDef == null)
                {
                    RhinoApp.WriteLine("Failed to retrieve instance definition.");
                    return null;
                }

                var curves = new List<Curve>();
                var breps = new List<Brep>();
                var meshes = new List<Mesh>();

                RhinoApp.WriteLine("[DEBUG] Contents of instance definition:");
                foreach (var objId in instanceDef.GetObjectIds())
                {
                    var obj = doc.Objects.Find(objId);
                    if (obj == null) continue;
                    var geo = obj.Geometry;
                    RhinoApp.WriteLine($"  - Type: {geo.GetType().Name}");

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
                        RhinoApp.WriteLine("[DEBUG] Converting block Extrusion to Brep.");
                        var extrusionBrep = ext.ToBrep();
                        if (extrusionBrep != null)
                            breps.Add(extrusionBrep);
                        else
                            RhinoApp.WriteLine("⚠️ Failed to convert Extrusion to Brep.");
                    }
                }

                if (curves.Count > 0)
                {
                    RhinoApp.WriteLine($"[DEBUG] {curves.Count} curve(s) found in block definition. Proceeding with pipe generation.");
                    double pipeRadius = 0.1;
                    Brep joinedPipe = ConvertMultipleCurvesToJoinedPipe(curves, doc, pipeRadius);
                    if (joinedPipe == null)
                    {
                        RhinoApp.WriteLine("Failed to generate pipe geometry from block curves.");
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
                    RhinoApp.WriteLine($"[DEBUG] Curve count: {curves.Count}, Brep count: {breps.Count}, Mesh count: {meshes.Count}");
                    RhinoApp.WriteLine("No supported geometry found in block definition.");
                    return null;
                }
            }
            else if (!(geometry is Brep) && !(geometry is Mesh))
            {
                RhinoApp.WriteLine($"Geometry type before conversion: {geometry.GetType().Name}");
                if (geometry is PolyCurve polyCurve)
                {
                    double pipeRadius = 0.1;
                    Brep joinedPipe = ConvertPolyCurveToBrep(polyCurve, doc, pipeRadius);
                    if (joinedPipe == null)
                    {
                        RhinoApp.WriteLine("Failed to convert polycurve to pipe brep.");
                        return null;
                    }
                    var pipeMeshes2 = Mesh.CreateFromBrep(joinedPipe, MeshingParameters.Default);
                    if (pipeMeshes2 == null || pipeMeshes2.Length == 0)
                    {
                        RhinoApp.WriteLine("❌ Failed to mesh the pipe brep.");
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
                        RhinoApp.WriteLine("Failed to convert single curve to pipe.");
                        return null;
                    }
                    var pipeMeshes3 = Mesh.CreateFromBrep(pipe[0], MeshingParameters.Default);
                    if (pipeMeshes3 == null || pipeMeshes3.Length == 0)
                    {
                        RhinoApp.WriteLine("❌ Failed to mesh the single pipe brep.");
                        return null;
                    }
                    return pipeMeshes3[0];
                }
                else
                {
                    RhinoApp.WriteLine("Selected geometry is not supported for export.");
                    return null;
                }
            }

            return geometry;
        }

        public static Guid AddTemporaryGeometryToDoc(RhinoDoc doc, GeometryBase geometry)
        {
            if (geometry == null)
            {
                RhinoApp.WriteLine("❌ Failed to prepare geometry for export.");
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
            RhinoApp.WriteLine($"[DEBUG] Geometry translated by {offset} to place its center at origin.");

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
                RhinoApp.WriteLine("⚠️ Temporary copy creation failed. Attempting export with original geometry.");
            }
            else
            {
                RhinoApp.WriteLine("✅ Temporary object added for export. Proceeding...");
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
                    RhinoApp.WriteLine($"[DEBUG] Temporary object {copyId} deleted.");
                }
                else
                {
                    RhinoApp.WriteLine($"[DEBUG] Temporary object {copyId} not found for deletion.");
                }
            }
            else
            {
                RhinoApp.WriteLine("[DEBUG] No temporary object to delete.");
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
            RhinoApp.WriteLine($"[DEBUG] File will be saved as: {fileName}");
            RhinoApp.WriteLine($"[DEBUG] Exporting object {objectId} to temporary path: {path}");
            ExportToVision.LastExportedUSDZPath = path;

            // Origin calculation block
            Point3d origin;
            var rhinoObj = doc.Objects.Find(objectId);
            if (rhinoObj is InstanceObject)
            {
                origin = CalcolaOrigineIstanzaBlocco(rhinoObj);
                RhinoApp.WriteLine($"[DEBUG] Origin from block instance center: {origin}");
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
                RhinoApp.WriteLine($"[DEBUG] Origin from geometry bounding box: {origin}");
            }

            string originString = $"{origin.X},{origin.Y},{origin.Z}";

            doc.Objects.UnselectAll();
            doc.Objects.Select(objectId);
            doc.Views.Redraw();

            string script = string.Format(
                "_-ExportWithOrigin {0} \"{1}\" _Enter _Enter",
                originString, path);

            RhinoApp.WriteLine($"[DEBUG] Export script command: {script}");
            RhinoApp.RunScript(script, false);

            bool fileExists = File.Exists(path);
            if (fileExists)
            {
                RhinoApp.WriteLine($"[DEBUG] File found after export: {path}");
            }
            else
            {
                RhinoApp.WriteLine($"[DEBUG] File NOT found after export: {path}");
            }

            return new ExportResult
            {
                Success = fileExists,
                Path = path,
                TemporaryCopyId = Guid.Empty
            };
        }

        public static Point3d CalcolaOrigineIstanzaBlocco(RhinoObject obj)
        {
            if (!(obj is InstanceObject instanceObj))
            {
                RhinoApp.WriteLine("L'oggetto selezionato non è un'istanza di blocco.");
                return Point3d.Unset;
            }

            var instanceDef = instanceObj.InstanceDefinition;
            if (instanceDef == null)
            {
                RhinoApp.WriteLine("Impossibile recuperare la definizione del blocco.");
                return Point3d.Unset;
            }

            var geometrieTrasformate = new List<GeometryBase>();
            foreach (var objId in instanceDef.GetObjectIds())
            {
                var rhinoObj = instanceObj.Document.Objects.Find(objId);
                if (rhinoObj == null) continue;

                var geo = rhinoObj.Geometry?.Duplicate();
                if (geo == null) continue;

                geo.Transform(instanceObj.InstanceXform);
                geometrieTrasformate.Add(geo);
            }

            if (geometrieTrasformate.Count == 0)
            {
                RhinoApp.WriteLine("Nessuna geometria trovata nella definizione del blocco.");
                return Point3d.Unset;
            }

            BoundingBox bboxCombinata = geometrieTrasformate[0].GetBoundingBox(true);
            for (int i = 1; i < geometrieTrasformate.Count; i++)
            {
                bboxCombinata.Union(geometrieTrasformate[i].GetBoundingBox(true));
            }

            return bboxCombinata.Center;
        }
    }
}