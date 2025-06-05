using System;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;

namespace Axys
{
    public class ScriptPipeMeshBlockCommand : Command
    {
        public static ScriptPipeMeshBlockCommand Instance { get; private set; }
        public override string EnglishName => "ScriptPipeMeshBlock";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
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

            doc.Objects.UnselectAll();

            foreach (var o in objRefs)
                doc.Objects.Select(o.ObjectId);

            doc.Views.Redraw();

            RhinoApp.RunScript("-_Pipe 0.3 0.3 _Enter _Enter", false);
            RhinoApp.RunScript("-_Mesh _Enter _Enter", false);

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

            doc.Objects.UnselectAll();
            foreach (var m in meshObjs)
                doc.Objects.Select(m.Id);

            var basePt = new Point3d(0, 0, 0);
            string idList = string.Join(" ", meshObjs.Select(m => m.Id));
            RhinoApp.RunScript($"-_Block {idList} {basePt.X},{basePt.Y},{basePt.Z} _Enter", false);

            Logger.LogDebug("Pipe → Mesh → Block pipeline completed.");

            return Result.Success;
        }
    }
}
