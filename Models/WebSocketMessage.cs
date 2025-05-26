using System.Collections.Generic;
using Newtonsoft.Json;

public class WebSocketMessage
{
    [JsonProperty("type")]
    public string Type { get; set; }  // currently expected: "error", "info"
    [JsonProperty("description")]
    public string Description { get; set; } //  e.g. Error description

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; } // Timestamp in milliseconds
}
