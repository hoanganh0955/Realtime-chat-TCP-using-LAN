using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerApp
{
    public class ServerManager
    {
        TcpListener listener;
        TcpClient client;
        Thread listenerThread;

        public void Start(int port, Action<string> log)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            log($"[Server] Listening on port {port}");
            listenerThread = new Thread(() =>
            {
                client = listener.AcceptTcpClient();
                log("[Server] Client connected");
                var ns = client.GetStream();
                var buf = new byte[1024];
                while (true)
                {
                    int r = ns.Read(buf, 0, buf.Length);
                    if (r <= 0) break;
                    string msg = Encoding.UTF8.GetString(buf, 0, r);
                    log(msg.Trim());
                }
            });
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        public void Send(string msg)
        {
            if (client == null) return;
            var ns = client.GetStream();
            var data = Encoding.UTF8.GetBytes(msg + "\n");
            ns.Write(data, 0, data.Length);
        }
    }
}
