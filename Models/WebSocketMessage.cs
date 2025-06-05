using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Simple DTO used for informational and error messages sent via WebSocket.
/// </summary>
public class WebSocketMessage
{
    [JsonProperty("type")]
    public string Type { get; set; }  // currently expected: "error", "info"
    [JsonProperty("description")]
    public string Description { get; set; } //  e.g. Error description

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; } // Timestamp in milliseconds
}
