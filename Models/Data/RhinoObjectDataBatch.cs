using System.Collections.Generic;
using Newtonsoft.Json;

namespace Axys.Models
{
    public class RhinoObjectDataBatch
    {
        [JsonProperty("type")]
        public string Type { get; set; }  // e.g., "batch_create"
        
        [JsonProperty("objects")]
        public List<RhinoObjectData> Objects { get; set; } // List of RhinoObjectData

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; } // Timestamp in milliseconds
    }
} 