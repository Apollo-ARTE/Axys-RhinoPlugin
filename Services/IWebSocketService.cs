using System.Threading.Tasks;

namespace Axys
{
    public interface IWebSocketService
    {
        void StartServer();
        void StopServer();
        bool IsServerRunning();
        void BroadcastMessage(string message);
        Task BroadcastBinary(byte[] data);
    }
}
