using Rhino;
using Rhino.Commands;
using Axys.Utilities;

namespace Axys.Commands
{
    public class TrackObjectCommand : Command
    {
        public static TrackObjectCommand Instance { get; private set; }

        public override string EnglishName => "TrackObject";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return CommandUtilities.ExecuteTrackObjectLogic(doc);
        }
    }
}
