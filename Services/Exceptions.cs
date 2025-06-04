
using System;

namespace RhinoPlugin
{
    public class GeometrySelectionException : Exception
    {
        public GeometrySelectionException(string message) : base(message) { }
        public GeometrySelectionException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class GeometryProcessingException : Exception
    {
        public GeometryProcessingException(string message) : base(message) { }
        public GeometryProcessingException(string message, Exception innerException) : base(message, innerException) { }
    }
}