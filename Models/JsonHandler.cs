using System.Collections.Generic;
using Newtonsoft.Json;

public class RhinoObjectDataBatch
{
    [JsonProperty("type")]
    public string Type { get; set; }  // e.g., "batch_create"
    [JsonProperty("objects")]
    public List<RhinoObjectData> Objects { get; set; } // List of RhinoObjectData

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; } // Timestamp in milliseconds
}

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

public class Center
{
    [JsonProperty("x")]
    public double X { get; set; }
    
    [JsonProperty("y")]
    public double Y { get; set; }
    
    [JsonProperty("z")]
    public double Z { get; set; }
}
public static class JsonHandler
{
    // Convert an object to JSON
    public static string Serialize(RhinoObjectData obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public static string SerializeBatch(RhinoObjectDataBatch obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    // Convert JSON to an object
    public static RhinoObjectData Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<RhinoObjectData>(json);
    }
}