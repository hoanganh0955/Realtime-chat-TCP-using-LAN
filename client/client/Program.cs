using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;

class Client
{
    static void Main()
    {
        Console.Title = "CLIENT CHAT + FILE";
        Console.WriteLine("=== CLIENT ===");

        Console.Write("Nhập IP Server: ");
        string serverIP = Console.ReadLine();
        int port = 5000;

        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        clientSocket.Connect(new IPEndPoint(IPAddress.Parse(serverIP), port));
        Console.WriteLine("✅ Đã kết nối tới Server!");

        // === Đăng nhập ===
        Console.Write("Tên đăng nhập: ");
        string username = Console.ReadLine();
        Console.Write("Mật khẩu: ");
        string password = Console.ReadLine();

        string loginInfo = $"{username}|{password}";
        clientSocket.Send(Encoding.UTF8.GetBytes(loginInfo));

        byte[] loginBuffer = new byte[1024];
        int loginReceived = clientSocket.Receive(loginBuffer);
        string loginResponse = Encoding.UTF8.GetString(loginBuffer, 0, loginReceived);
        Console.WriteLine(loginResponse);

        if (loginResponse.Contains("Sai mật khẩu"))
        {
            Console.WriteLine("Thoát chương trình...");
            clientSocket.Close();
            return;
        }

        // === Luồng nhận tin nhắn từ server ===
        new Thread(() =>
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[8192];
                    int received = clientSocket.Receive(buffer);
                    if (received == 0) break;
                    string msg = Encoding.UTF8.GetString(buffer, 0, received);

                    if (msg == "/server_exit")
                    {
                        Console.WriteLine("\n⚠️ Server đã ngắt kết nối.");
                        clientSocket.Close();
                        Environment.Exit(0);
                    }

                    // XÓA HOÀN TOÀN DÒNG PROMPT HIỆN TẠI
                    Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");

                    // Hiển thị tin nhắn từ server
                    Console.WriteLine($"{msg}");

                    // Hiển thị prompt mới
                    Console.Write("Bạn: ");
                }
            }
            catch
            {
                Console.WriteLine("\n❌ Mất kết nối với server!");
                Environment.Exit(0);
            }
        }).Start();

        // === Gửi tin nhắn hoặc file ===
        while (true)
        {
            Console.Write("Bạn: ");
            string message = Console.ReadLine();

            if (message.ToLower() == "exit")
            {
                clientSocket.Close();
                Environment.Exit(0);
            }

            // Gửi file
            if (message.StartsWith("/file "))
            {
                try
                {
                    string path = message.Substring(6);
                    if (!File.Exists(path))
                    {
                        Console.WriteLine("❌ File không tồn tại!");
                        continue;
                    }

                    FileInfo fileInfo = new FileInfo(path);
                    if (fileInfo.Length > 5 * 1024 * 1024) // 5MB
                    {
                        Console.WriteLine("❌ File quá lớn (giới hạn 5MB)!");
                        continue;
                    }

                    string fileName = Path.GetFileName(path);
                    byte[] fileBytes = File.ReadAllBytes(path);
                    string base64 = Convert.ToBase64String(fileBytes);

                    string packet = $"/file|{fileName}|{base64}";
                    clientSocket.Send(Encoding.UTF8.GetBytes(packet));
                    Console.WriteLine($"📤 Đã gửi file: {fileName} ({fileBytes.Length} bytes)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Lỗi gửi file: {ex.Message}");
                }
                continue;
            }

            // Gửi tin nhắn văn bản
            clientSocket.Send(Encoding.UTF8.GetBytes(message));
        }
    }
}