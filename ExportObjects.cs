using System;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace ExportToUsdzCommand
{
    public class ExportToUsdzCommand : Command
    {
        public ExportToUsdzCommand()
        {
            Instance = this;
        }

        public static ExportToUsdzCommand Instance { get; private set; }

        public override string EnglishName => "ExportToUsdz";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Seleziona gli oggetti da esportare
            var go = new GetObject();
            go.SetCommandPrompt("Seleziona gli oggetti da esportare");
            go.SubObjectSelect = false;
            go.GroupSelect = true;
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var selectedObjects = go.Objects();
            foreach (var objRef in selectedObjects)
            {
                doc.Objects.Select(objRef.ObjectId, true);
            }

            // Specifica il percorso del file USDZ di destinazione
            string filePath = "/Users/iliadev/Downloads/Object_{objectId}.usdz";

            // Costruisce il comando di esportazione
            string exportCommand = $"-_Export \"{filePath}\" _Enter";

            // Esegue il comando di esportazione
            bool commandResult = RhinoApp.RunScript(exportCommand, false);
            if (!commandResult)
            {
                RhinoApp.WriteLine("Error while exporting objects");
                return Result.Failure;
            }

            // Deseleziona gli oggetti dopo l'esportazione
            doc.Objects.UnselectAll();

            RhinoApp.WriteLine($"Export success: {filePath}");
            return Result.Success;
        }
    }
}