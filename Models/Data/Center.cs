using Newtonsoft.Json;

namespace Axys.Models
{
    public class Center
    {
        [JsonProperty("x")]
        public double X { get; set; }
        
        [JsonProperty("y")]
        public double Y { get; set; }
        
        [JsonProperty("z")]
        public double Z { get; set; }
    }
} 