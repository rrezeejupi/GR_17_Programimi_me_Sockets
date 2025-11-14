using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

class Program
{
    static IPAddress SERVER_IP = IPAddress.Any; 
    static int SERVER_PORT = 9000;
    static int MAX_ACTIVE_CONNECTIONS = 4;
    static int IDLE_TIMEOUT_SECONDS = 500;
    static string STORAGE_DIR = Path.Combine(AppContext.BaseDirectory, "ServerStorage");

    static TcpListener listener;
    static ConcurrentDictionary<string, ClientState> clients = new();
    static long totalBytesReceived = 0;
    static long totalBytesSent = 0;
    static BlockingCollection<CommandItem> commandQueue = new();

    static async Task Main()
    {
        Directory.CreateDirectory(STORAGE_DIR);
        listener = new TcpListener(SERVER_IP, SERVER_PORT);
        listener.Start();
        Console.WriteLine($"[SERVER] Listening on {SERVER_IP}:{SERVER_PORT} (max {MAX_ACTIVE_CONNECTIONS} clients)");

        _ = Task.Run(CommandProcessorLoop);
        _ = Task.Run(IdleScannerLoop);
        _ = Task.Run(TrafficMonitorLoop); 

        while (true)
        {
            TcpClient tcp = await listener.AcceptTcpClientAsync();

            if (ActiveConnectionsCount() >= MAX_ACTIVE_CONNECTIONS)
            {
                using var s = tcp.GetStream();
                var msg = Encoding.UTF8.GetBytes("BUSY:Server at capacity. Try later.\n");
                await s.WriteAsync(msg, 0, msg.Length);
                tcp.Close();
                continue;
            }

            var state = new ClientState(tcp);
            clients[state.Id] = state;
            Console.WriteLine($"[SERVER] Accepted {state.Id} ({state.IP})");

            _ = Task.Run(() => HandleClient(state));
        }
    }

    static int ActiveConnectionsCount()
    {
        int count = 0;
        foreach (var kv in clients)
            if (kv.Value.IsConnected) count++;
        return count;
    }

    static async Task HandleClient(ClientState st){}

    static void CloseClient(string id, string reason){}
    static async Task CommandProcessorLoop()
    {
    }
    

    static string BuildStatsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Active connections: {ActiveConnectionsCount()}");
        sb.AppendLine("Clients:");
        foreach (var kv in clients)
            sb.AppendLine($"- {kv.Value.Username}@{kv.Value.IP} Messages={kv.Value.MessageCount}");
        sb.AppendLine($"Total bytes received: {totalBytesReceived}");
        sb.AppendLine($"Total bytes sent: {totalBytesSent}");
        return sb.ToString();
    }

    static async Task TrafficMonitorLoop(){}
    static async Task IdleScannerLoop(){}

    class ClientState
{
    public string Id { get; }
        public TcpClient Tcp { get; }
        public NetworkStream Stream => Tcp.GetStream();
        public string IP { get; }
        public string Username { get; set; } = "unknown";
        public Role Role { get; set; } = Role.ReadOnly;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public bool IsConnected { get; set; } = true;
        public int MessageCount { get; set; } = 0;
        public long BytesReceived { get; set; } = 0;
        public long BytesSent { get; set; } = 0;
     public ClientState(TcpClient tcp)
        {
            Tcp = tcp;
            Id = Guid.NewGuid().ToString().Substring(0, 8);
            IP = tcp.Client.RemoteEndPoint?.ToString() ?? "unknown";
        }
}

      enum Role { Admin, ReadOnly }

      class CommandItem
{
    
}

}

