using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ServerApp
{
    public class MainForm : Form
    {
        RadioButton rbServer;
        RadioButton rbClient;
        TextBox txtIP;
        TextBox txtPort;
        TextBox txtUsername;
        Button btnStartConnect;
        RichTextBox rtbLog;
        TextBox txtMessage;
        Button btnSend;
        Button btnEmoji;

        TcpListener? listener;
        TcpClient? serverClient;
        TcpClient? client;
        NetworkStream? clientStream;
        Thread? listenerThread;
        Thread? receiveThread;
        CancellationTokenSource cts = new CancellationTokenSource();

        bool isServer => rbServer.Checked;

        public MainForm()
        {
            this.Text = "Realtime Chat (1-to-1)";
            this.Width = 750;
            this.Height = 540;
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeComponents();

           
            try
            {
                Console.Title = "Chat Server Log";
                Console.WriteLine("[CLI] Console initialized.");
            }
            catch { }
        }

        void InitializeComponents()
        {
            rbServer = new RadioButton() { Left = 20, Top = 15, Width = 80, Text = "Server", Checked = true };
            rbClient = new RadioButton() { Left = 120, Top = 15, Width = 80, Text = "Client" };

            var lblIP = new Label() { Left = 220, Top = 18, Text = "IP:", Width = 25 };
            txtIP = new TextBox() { Left = 250, Top = 15, Width = 130, Text = "127.0.0.1" };

            var lblPort = new Label() { Left = 390, Top = 18, Text = "Port:", Width = 35 };
            txtPort = new TextBox() { Left = 430, Top = 15, Width = 60, Text = "9000" };

            var lblUser = new Label() { Left = 500, Top = 18, Text = "Username:", Width = 70 };
            txtUsername = new TextBox() { Left = 580, Top = 15, Width = 100, Text = "User" + new Random().Next(1000, 9999) };

            btnStartConnect = new Button() { Left = 690, Top = 12, Width = 40, Text = "Start" };

            rtbLog = new RichTextBox()
            {
                Left = 20,
                Top = 60,
                Width = 710,
                Height = 350,
                ReadOnly = true
            };

            txtMessage = new TextBox() { Left = 20, Top = 430, Width = 540, Height = 25 };
            btnSend = new Button() { Left = 570, Top = 428, Width = 70, Height = 30, Text = "Send" };
            btnEmoji = new Button() { Left = 650, Top = 428, Width = 80, Height = 30, Text = "😊" };

            this.Controls.AddRange(new Control[]
            {
                rbServer, rbClient, lblIP, txtIP, lblPort, txtPort, lblUser, txtUsername, btnStartConnect,
                rtbLog, txtMessage, btnSend, btnEmoji
            });

            btnStartConnect.Click += BtnStartConnect_Click;
            btnSend.Click += BtnSend_Click;
            btnEmoji.Click += BtnEmoji_Click;
        }

        private void BtnStartConnect_Click(object? sender, EventArgs e)
        {
            if (btnStartConnect.Text == "Start" || btnStartConnect.Text == "Connect")
            {
                if (rbServer.Checked)
                {
                    StartServer();
                    btnStartConnect.Text = "Stop";
                }
                else
                {
                    StartClient();
                    btnStartConnect.Text = "Disconnect";
                }

                rbServer.Enabled = rbClient.Enabled = false;
            }
            else
            {
                StopConnection();
                btnStartConnect.Text = rbServer.Checked ? "Start" : "Connect";
                rbServer.Enabled = rbClient.Enabled = true;
            }
        }

        // ===== SERVER =====
        void StartServer()
        {
            try
            {
                int port = int.Parse(txtPort.Text);
                listener = new TcpListener(IPAddress.Any, port);
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start();
                Log($"[Server] Listening on port {port}");
                listenerThread = new Thread(ServerAcceptLoop);
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            catch (Exception ex)
            {
                Error("Server Start", ex);
            }
        }

        void ServerAcceptLoop()
        {
            try
            {
                serverClient = listener!.AcceptTcpClient();
                Log("[Server] Client connected: " + serverClient.Client.RemoteEndPoint);
                var ns = serverClient.GetStream();
                ReceiveLoop(ns);
            }
            catch (Exception ex)
            {
                Error("AcceptLoop", ex);
            }
        }

        void SendToClient(string msg)
        {
            try
            {
                if (serverClient == null) return;
                var ns = serverClient.GetStream();
                var data = Encoding.UTF8.GetBytes(msg + "\n");
                ns.Write(data, 0, data.Length);
                if (isServer) Console.WriteLine("[SEND→Client] " + msg);
            }
            catch (Exception ex)
            {
                Error("SendToClient", ex);
            }
        }

        // ===== CLIENT =====
        void StartClient()
        {
            try
            {
                string ip = txtIP.Text;
                int port = int.Parse(txtPort.Text);
                client = new TcpClient();
                client.Connect(ip, port);
                clientStream = client.GetStream();
                Log("[Client] Connected to server " + ip + ":" + port);
                receiveThread = new Thread(() => ReceiveLoop(clientStream));
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Error("StartClient", ex);
            }
        }

        void ReceiveLoop(NetworkStream? ns)
        {
            try
            {
                var buf = new byte[1024];
                var sb = new StringBuilder();
                while (true)
                {
                    int r = ns!.Read(buf, 0, buf.Length);
                    if (r <= 0) break;
                    string text = Encoding.UTF8.GetString(buf, 0, r);
                    sb.Append(text);
                    int idx;
                    string content = sb.ToString();
                    while ((idx = content.IndexOf('\n')) >= 0)
                    {
                        string line = content.Substring(0, idx).Trim();
                        content = content.Substring(idx + 1);
                        AppendLog(line);
                        if (isServer) Console.WriteLine("[RECV] " + line);
                    }
                    sb = new StringBuilder(content);
                }
            }
            catch (Exception ex)
            {
                Error("ReceiveLoop", ex);
            }
        }

        // ===== UI =====
        private void BtnSend_Click(object? sender, EventArgs e)
        {
            string msg = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            string username = txtUsername.Text.Trim();
            string fullMsg = $"{username}: {msg}";

            if (rbServer.Checked)
                SendToClient(fullMsg);
            else
                SendToServer(fullMsg);

            AppendLog("Me: " + msg);
            txtMessage.Clear();
        }

        void SendToServer(string msg)
        {
            try
            {
                if (clientStream == null) return;
                var data = Encoding.UTF8.GetBytes(msg + "\n");
                clientStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Error("SendToServer", ex);
            }
        }

        private void BtnEmoji_Click(object? sender, EventArgs e)
        {
            var f = new Form() { Width = 220, Height = 80, FormBorderStyle = FormBorderStyle.FixedToolWindow, StartPosition = FormStartPosition.CenterParent };
            string[] emojis = new string[] { "😊", "😂", "😍", "👍", "😮", "😢", "😡", "🎉" };
            int x = 10;
            foreach (var em in emojis)
            {
                var b = new Button() { Left = x, Top = 10, Width = 24, Height = 24, Text = em, Font = this.Font };
                b.Click += (s, ev) => { txtMessage.Text += em; f.Close(); };
                f.Controls.Add(b);
                x += 28;
            }
            f.ShowDialog(this);
        }

        // ===== HELPERS =====
        void StopConnection()
        {
            try
            {
                cts.Cancel();
                listener?.Stop();
                serverClient?.Close();
                client?.Close();
                clientStream?.Close();
                Log("[System] Connection stopped.");
                if (isServer) Console.WriteLine("[INFO] All connections closed.");
            }
            catch (Exception ex)
            {
                Error("StopConnection", ex);
            }
        }

        void Log(string s)
        {
            if (InvokeRequired) { Invoke(new Action<string>(Log), s); return; }
            rtbLog.AppendText(s + Environment.NewLine);
            if (isServer) Console.WriteLine(s);
        }

        void Error(string where, Exception ex)
        {
            string msg = $"[ERROR] {where}: {ex.Message}";
            if (InvokeRequired) { Invoke(new Action<string>(Log), msg); return; }
            rtbLog.AppendText(msg + Environment.NewLine);
            if (isServer)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                Console.ResetColor();
            }
        }

        void AppendLog(string s) => Log(s);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            StopConnection();
        }
    }
}
