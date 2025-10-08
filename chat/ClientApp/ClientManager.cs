using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientApp
{
    public class ClientManager
    {
        TcpClient client;
        NetworkStream stream;
        Thread receiveThread;

        public void Connect(string ip, int port, Action<string> log)
        {
            client = new TcpClient();
            client.Connect(ip, port);
            stream = client.GetStream();
            log($"[Client] Connected to {ip}:{port}");
            receiveThread = new Thread(() =>
            {
                var buf = new byte[1024];
                while (true)
                {
                    int r = stream.Read(buf, 0, buf.Length);
                    if (r <= 0) break;
                    string msg = Encoding.UTF8.GetString(buf, 0, r);
                    log(msg.Trim());
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        public void Send(string msg)
        {
            var data = Encoding.UTF8.GetBytes(msg + "\n");
            stream.Write(data, 0, data.Length);
        }
    }
}
