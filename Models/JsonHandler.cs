using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Represents a collection of <see cref="RhinoObjectData"/> messages.
/// </summary>
public class RhinoObjectDataBatch
{
    [JsonProperty("type")]
    public string Type { get; set; }  // e.g., "batch_create"
    [JsonProperty("objects")]
    public List<RhinoObjectData> Objects { get; set; } // List of RhinoObjectData

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; } // Timestamp in milliseconds
}

/// <summary>
/// Serializable description of a single Rhino object.
/// </summary>
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

/// <summary>
/// Simple structure used for JSON serialization of a 3D point.
/// </summary>
public class Center
{
    [JsonProperty("x")]
    public double X { get; set; }
    
    [JsonProperty("y")]
    public double Y { get; set; }
    
    [JsonProperty("z")]
    public double Z { get; set; }
}
/// <summary>
/// Utility methods for serializing and deserializing message objects.
/// </summary>
public static class JsonHandler
{
    /// <summary>
    /// Converts a <see cref="RhinoObjectData"/> instance to JSON.
    /// </summary>
    public static string Serialize(RhinoObjectData obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    /// <summary>
    /// Converts a <see cref="RhinoObjectDataBatch"/> to JSON.
    /// </summary>
    public static string SerializeBatch(RhinoObjectDataBatch obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    /// <summary>
    /// Parses JSON into a <see cref="RhinoObjectData"/> instance.
    /// </summary>
    /// <param name="json">JSON string.</param>
    public static RhinoObjectData Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<RhinoObjectData>(json);
    }
}