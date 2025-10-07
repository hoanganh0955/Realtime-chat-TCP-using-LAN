using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;

class Server
{
    static void Main()
    {
        Console.Title = "SERVER CHAT + FILE";
        Console.WriteLine("=== SERVER ===");

        string localIP = "127.0.0.1";
        int port = 5000;

        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(localIP), port);
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        serverSocket.Bind(endPoint);
        serverSocket.Listen(1);
        Console.WriteLine($"Dang lang nghe tai {localIP}:{port}");

        Socket clientSocket = serverSocket.Accept();
        Console.WriteLine("Client da ket noi!");

        // === Đăng nhập ===
        byte[] loginBuffer = new byte[1024];
        int loginBytes = clientSocket.Receive(loginBuffer);
        string loginInfo = Encoding.UTF8.GetString(loginBuffer, 0, loginBytes);
        string[] parts = loginInfo.Split('|');

        string username = parts[0];
        string password = parts[1];

        if (password != "123")
        {
            clientSocket.Send(Encoding.UTF8.GetBytes("❌ Sai mật khẩu"));
            clientSocket.Close();
            return;
        }

        clientSocket.Send(Encoding.UTF8.GetBytes("✅ Đăng nhập thành công"));
        Console.WriteLine($"🔓 {username} đã đăng nhập.");

        // === Tạo luồng nhận dữ liệu từ client ===
        new Thread(() =>
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[1024 * 1024]; // Tăng buffer lên 1MB
                    int received = clientSocket.Receive(buffer);
                    if (received == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, received);

                    // Nếu là file
                    if (message.StartsWith("/file|"))
                    {
                        try
                        {
                            string[] fileParts = message.Split('|');
                            if (fileParts.Length >= 3)
                            {
                                string fileName = fileParts[1];
                                string fileData = fileParts[2];

                                // Kiểm tra kích thước file
                                if (fileData.Length > 10000000) // ~10MB
                                {
                                    Console.WriteLine($"❌ File {fileName} quá lớn!");
                                    continue;
                                }

                                byte[] fileBytes = Convert.FromBase64String(fileData);
                                File.WriteAllBytes($"received_{fileName}", fileBytes);
                                Console.WriteLine($"📁 {username} đã gửi file: {fileName} ({fileBytes.Length} bytes)");
                            }
                        }
                        catch (Exception fileEx)
                        {
                            Console.WriteLine($"❌ Lỗi nhận file: {fileEx.Message}");
                        }
                        continue;
                    }

                    Console.WriteLine($"{username}: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Client đã ngắt kết nối: {ex.Message}");
            }
        }).Start();

        // === Server gửi tin nhắn ngược lại ===
        while (true)
        {
            string reply = Console.ReadLine();
            if (reply.ToLower() == "exit")
            {
                clientSocket.Send(Encoding.UTF8.GetBytes("/server_exit"));
                break;
            }

            clientSocket.Send(Encoding.UTF8.GetBytes($"Server: {reply}"));
        }

        clientSocket.Close();
        serverSocket.Close();
    }
}