using System;
using Rhino;
using Rhino.DocObjects;
using Rhino.Input;
using RhinoPlugin;
using Rhino.Geometry;
using System.Collections.Generic;
using Rhino.Commands;
using System.Linq;

namespace RhinoPlugin
{
    public static class SelectionObjectManager
    {
        public static Guid EnsureObjectIsSelected(RhinoDoc doc, Guid currentId)
        {
            if (currentId == Guid.Empty)
            {
                RhinoApp.WriteLine("No preselected object found. Attempting fallback user selection...");
                RhinoApp.WriteLine($"[DEBUG BEFORE FALLBACK] Current SelectedObjectId: {currentId}");

                var rc = RhinoGet.GetOneObject("Select object to export", false, ObjectType.AnyObject, out ObjRef fallbackRef);
                if (rc == Result.Success && fallbackRef?.Object() != null)
                {
                    var fallbackId = fallbackRef.Object().Id;
                    RhinoApp.WriteLine($"[DEBUG] Fallback selection ID: {fallbackId}");
                    RhinoApp.WriteLine("Selected object via fallback: " + fallbackId);
                    return fallbackId;
                }

                RhinoApp.WriteLine($"[DEBUG] RhinoGet.GetOneObject result: {rc}");
                RhinoApp.WriteLine("Fallback selection failed.");
                return currentId;
            }
            return currentId;
        }

        public static void ProcessInstanceReference(InstanceReferenceGeometry instanceRef, RhinoDoc doc, List<Brep> breps, List<Mesh> meshes, List<Curve> curves)
        {
            var instanceDef = FindInstanceDefinitionByGuid(doc, instanceRef.ParentIdefId);
            if (instanceDef == null) return;

            foreach (var id in instanceDef.GetObjectIds())
            {
                var blockObj = doc.Objects.Find(id);
                if (blockObj == null) continue;

                GeometryBase blockGeo = blockObj.Geometry;
                if (blockGeo is Brep brep)
                {
                    breps.Add(brep.DuplicateBrep());
                }
                else if (blockGeo is Mesh mesh)
                {
                    meshes.Add(mesh.DuplicateMesh());
                }
                else if (blockGeo is Curve curve)
                {
                    curves.Add(curve.DuplicateCurve());
                }
            }
        }

        // Helper method to retrieve an instance definition by its Guid (ID)
        // This avoids any ambiguity with method overloads expecting string names
        public static InstanceDefinition FindInstanceDefinitionByGuid(RhinoDoc doc, Guid id)
        {
            return doc.InstanceDefinitions.FirstOrDefault(def => def.Id == id);
        }
    }
}