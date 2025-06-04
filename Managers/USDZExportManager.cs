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
            RhinoApp.WriteLine($"[DEBUG] Preparing to send {fileBytes.Length} bytes via Axys.");
                // Chunk and send the file via WebSocket
                int chunkSize = 1024 * 32; // 32 KB
                int offset = 0;
                int totalSize = fileBytes.Length;
                RhinoApp.WriteLine($"[DEBUG] Chunked transmission starting. Total size: {totalSize} bytes.");

                while (offset < totalSize)
                {
                    int currentChunkSize = Math.Min(chunkSize, totalSize - offset);
                    byte[] chunk = new byte[currentChunkSize];
                    Array.Copy(fileBytes, offset, chunk, 0, currentChunkSize);
                    bool isLastChunk = (offset + currentChunkSize) >= totalSize;

                    try
                    {
                        await WebSocketServerManager.BroadcastBinary(chunk);
                        RhinoApp.WriteLine($"[DEBUG] Sent chunk: Offset={offset}, Size={currentChunkSize}, Last={isLastChunk}");
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"[ERROR] Failed to send chunk at offset {offset}: {ex.Message}");
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
                RhinoApp.WriteLine($"[DEBUG] Sending metadata: {metadata}");
                WebSocketServerManager.BroadcastMessage(metadata);
                RhinoApp.WriteLine("📤 USDZ file broadcasted to Axys clients.");
            
        }
    }
}