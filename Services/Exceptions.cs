
using System;

namespace RhinoPlugin
{
    /// <summary>
    /// Thrown when geometry cannot be selected or resolved.
    /// </summary>
    public class GeometrySelectionException : Exception
    {
        public GeometrySelectionException(string message) : base(message) { }
        public GeometrySelectionException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when geometry processing fails during export or conversion.
    /// </summary>
    public class GeometryProcessingException : Exception
    {
        public GeometryProcessingException(string message) : base(message) { }
        public GeometryProcessingException(string message, Exception innerException) : base(message, innerException) { }
    }
}