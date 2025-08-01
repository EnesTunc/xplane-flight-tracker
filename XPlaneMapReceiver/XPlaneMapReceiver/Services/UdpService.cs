using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;

namespace XPlaneMapReceiver.Services
{
    public class UdpService
    {
        private UdpClient udpClient;
        private bool isRunning = false;

        public event Action<string> DataReceived;

        public async Task StartListening(int port)
        {
            if (isRunning) return;

            try
            {
                udpClient = new UdpClient(port);
                isRunning = true;

                while (isRunning)
                {
                    var result = await udpClient.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    DataReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                // Handle exception
                Console.WriteLine($"UDP Error: {ex.Message}");
            }
        }

        public void StopListening()
        {
            isRunning = false;
            udpClient?.Close();
        }

        public void Dispose()
        {
            StopListening();
            udpClient?.Dispose();
        }
    }
}