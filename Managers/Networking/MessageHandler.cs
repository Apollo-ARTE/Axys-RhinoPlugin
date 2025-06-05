using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Axys.Managers.Networking
{
    /// <summary>
    /// Provides helper methods for creating JSON messages exchanged via WebSocket.
    /// </summary>
    public class MessageHandler
    {
        /// <summary>
        /// Creates a <see cref="WebSocketMessage"/> and serializes it to JSON.
        /// </summary>
        /// <param name="type">Message type such as <c>info</c> or <c>error</c>.</param>
        /// <param name="description">Human readable message text.</param>
        /// <returns>JSON encoded message.</returns>
        public static string CreateAndSerializeMessage(string type, string description)
        {
            var message = new WebSocketMessage
            {
                Type = type,
                Description = description,
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };

            return JsonConvert.SerializeObject(message);
        }
    }
}
