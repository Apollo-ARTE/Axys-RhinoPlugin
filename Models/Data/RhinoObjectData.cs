using Newtonsoft.Json;

namespace Axys.Models
{
    public class RhinoObjectData
    {
        [JsonProperty("type")]
        public string Type { get; set; }  // e.g., "create"

        [JsonProperty("objectName")]
        public string ObjectName { get; set; }  // e.g., "table"

        [JsonProperty("objectId")]
        public string ObjectId { get; set; } // e.g., "Sphere_123"

        [JsonProperty("center")]
        public Center Center { get; set; } // Nested object for center coordinates

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; } // Timestamp in milliseconds
    }
} 