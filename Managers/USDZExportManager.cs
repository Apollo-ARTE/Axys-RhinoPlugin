using System;
using System.IO;
using Newtonsoft.Json;
using Rhino;
using System.Threading.Tasks;
using Axys;

namespace Axys
{
    public static class USDZExportManager
    {
        public static async Task ExecuteExportAsync(byte[] fileBytes, string filePath)
        {
            Logger.LogDebug($" Preparing to send {fileBytes.Length} bytes via Axys.");
            // Chunk and send the file via WebSocket
            int chunkSize = 1024 * 32; // 32 KB
            int offset = 0;
            int totalSize = fileBytes.Length;
            Logger.LogDebug($" Chunked transmission starting. Total size: {totalSize} bytes.");

            while (offset < totalSize)
            {
                int currentChunkSize = Math.Min(chunkSize, totalSize - offset);
                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(fileBytes, offset, chunk, 0, currentChunkSize);
                bool isLastChunk = (offset + currentChunkSize) >= totalSize;

                try
                {
                    await WebSocketServerManager.BroadcastBinary(chunk);
                    Logger.LogDebug($"Sent chunk: Offset={offset}, Size={currentChunkSize}, Last={isLastChunk}");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Failed to send chunk at offset {offset}: {ex.Message}");
                }

                offset += currentChunkSize;
            }
            // Also send metadata with the export (e.g., file size, name)
            string fileName = Path.GetFileName(filePath);
            string metadata = JsonConvert.SerializeObject(new
            {
                type = "usdz_metadata",
                fileName = fileName,
                size = fileBytes.Length,
                timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            });
            Logger.LogDebug($"Sending metadata: {metadata}");
            WebSocketServerManager.BroadcastMessage(metadata);
            Logger.LogInfo($"File {fileName} broadcasted to Axys client.");
            RhinoApp.WriteLine($"File {fileName} broadcasted to Axys client.");
            
        }
    }
}