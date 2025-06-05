using System;
using Rhino;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Geometry;
using System.Collections.Generic;
using Rhino.Commands;
using System.Linq;

namespace Axys.Managers.ObjectHandling
{
    /// <summary>
    /// Helper methods for selecting geometry and resolving instance definitions.
    /// </summary>
    public static class SelectionObjectManager
    {
        /// <summary>
        /// Ensures an object is selected, optionally prompting the user.
        /// </summary>
        /// <param name="doc">Active Rhino document.</param>
        /// <param name="currentId">Pre-selected object identifier.</param>
        /// <returns>The confirmed object identifier or <see cref="Guid.Empty"/> if selection failed.</returns>
        public static Guid EnsureObjectIsSelected(RhinoDoc doc, Guid currentId)
        {
            if (currentId == Guid.Empty)
            {
                Logger.LogWarning("No preselected object found. Attempting fallback user selection...");

                var rc = RhinoGet.GetOneObject("Select object to export", false, ObjectType.AnyObject, out ObjRef fallbackRef);
                if (rc == Result.Success && fallbackRef?.Object() != null)
                {
                    var fallbackId = fallbackRef.Object().Id;
                    Logger.LogInfo($" Selected object via fallback: {fallbackId}");
                    return fallbackId;
                }

                Logger.LogDebug($" RhinoGet.GetOneObject result: {rc}");
                Logger.LogError("Fallback selection failed.");
                return currentId;
            }
            return currentId;
        }

        /// <summary>
        /// Expands an instance reference and collects its geometry into separate lists.
        /// </summary>
        /// <param name="instanceRef">Reference geometry pointing to the block instance.</param>
        /// <param name="doc">Active Rhino document.</param>
        /// <param name="breps">Collection receiving Brep geometry.</param>
        /// <param name="meshes">Collection receiving Mesh geometry.</param>
        /// <param name="curves">Collection receiving Curve geometry.</param>
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

        /// <summary>
        /// Retrieves an instance definition by Guid to avoid name collisions.
        /// </summary>
        /// <param name="doc">Active Rhino document.</param>
        /// <param name="id">Identifier of the instance definition.</param>
        /// <returns>The instance definition or null if not found.</returns>
        public static InstanceDefinition FindInstanceDefinitionByGuid(RhinoDoc doc, Guid id)
        {
            return doc.InstanceDefinitions.FirstOrDefault(def => def.Id == id);
        }
    }
}
