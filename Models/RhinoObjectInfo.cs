using System;
namespace Axys.Models
{
    /// <summary>
    /// Lightweight container used to expose basic object information to clients.
    /// </summary>
    public class RhinoObjectInfo
    {
        /// <summary>Unique identifier of the object.</summary>
        public Guid Id { get; set; }
        /// <summary>Display name.</summary>
        public string Name { get; set; }
        /// <summary>Type of Rhino object.</summary>
        public string ObjectType { get; set; }
    }
}
