using Newtonsoft.Json;

public class RhinoObjectData
{
    [JsonProperty("type")]
    public string Type { get; set; }  // e.g., "create"

    [JsonProperty("objectId")]
    public string ObjectId { get; set; } // e.g., "Sphere_123"

    [JsonProperty("center")]
    public Center Center { get; set; } // Nested object for center coordinates

    [JsonProperty("radius")]
    public double Radius { get; set; } // Radius of the sphere

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

    // Convert JSON to an object
    public static RhinoObjectData Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<RhinoObjectData>(json);
    }
}