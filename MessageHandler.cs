using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class MessageHandler {
  
  public static string CreateAndSerializeMessage(string type, string description) {
    
        var message = new WebSocketMessage
        {
            Type = type,
            Description = description,
            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        };

        return JsonConvert.SerializeObject(message);
    }
}