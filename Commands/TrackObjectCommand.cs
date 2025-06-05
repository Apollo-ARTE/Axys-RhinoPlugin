using Rhino;
using Rhino.Commands;
using Axys.Utilities;

namespace Axys.Commands
{
    /// <summary>
    /// Wraps <see cref="CommandUtilities.ExecuteTrackObjectLogic"/> in a Rhino command
    /// so object tracking can be invoked interactively.
    /// </summary>
    public class TrackObjectCommand : Command
    {
        /// <summary>Singleton instance created by Rhino.</summary>
        public static TrackObjectCommand Instance { get; private set; }

        public override string EnglishName => "TrackObject";

        /// <summary>
        /// Executes the tracking routine and sends object information over WebSocket.
        /// </summary>
        /// <param name="doc">Active Rhino document.</param>
        /// <param name="mode">Execution mode.</param>
        /// <returns>Result of the tracking operation.</returns>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return CommandUtilities.ExecuteTrackObjectLogic(doc);
        }
    }
}
