using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Axys.Managers.ObjectHandling;

namespace Axys.Managers.Geometry
{
    public static class GeometryConversion
    {
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
    }
}
