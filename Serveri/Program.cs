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
    static BlockingCollection<CommandItem> adminQueue = new();
    static BlockingCollection<CommandItem> userQueue = new();

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
                Console.WriteLine("[SERVER] REFUSED CONNECTION → Server is busy (max clients reached)");
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

    static async Task HandleClient(ClientState st)
    {
        try
        {
            var stream = st.Stream;
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };


            string hello = await reader.ReadLineAsync();
            if (hello == null || !hello.StartsWith("HELLO "))
            {
                CloseClient(st.Id, "Bad handshake");
                return;
            }

            var parts = hello.Split(' ', 3);
            st.Username = parts[1];
            st.Role = parts[2].Trim().ToLower() == "admin" ? Role.Admin : Role.ReadOnly;
            await writer.WriteLineAsync($"WELCOME {st.Username}. Role={st.Role}");
            Console.WriteLine($"[SERVER] {st.Id} identified as {st.Username} ({st.Role})");

            while (st.IsConnected)
            {
                var readTask = reader.ReadLineAsync();
                var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(IDLE_TIMEOUT_SECONDS)));
                if (completed != readTask)
                {
                    await writer.WriteLineAsync("TIMEOUT:No activity, disconnecting.");
                    break;
                }

                string line = readTask.Result;
                if (line == null) break;

                st.LastSeen = DateTime.UtcNow;
                st.MessageCount++;
                totalBytesReceived += Encoding.UTF8.GetByteCount(line);

                var cmdItem = new CommandItem { Client = st, CommandLine = line, Writer = writer };
                if (st.Role == Role.Admin)
                    adminQueue.Add(cmdItem);
                else
                    userQueue.Add(cmdItem);
            }
        }
        catch { }
        finally { CloseClient(st.Id, "Disconnected"); }
    }

    static void CloseClient(string id, string reason)
    {
        if (clients.TryRemove(id, out var s))
        {
            try { s.Tcp.Close(); } catch { }
            s.IsConnected = false;
            Console.WriteLine($"[SERVER] Closed {id}: {reason}");
        }
    }
    static async Task CommandProcessorLoop()
    {

        while (true)
        {
            CommandItem item = null;

            if (adminQueue.TryTake(out item, TimeSpan.FromMilliseconds(20)))
            {
            }
            else
            {
                userQueue.TryTake(out item, TimeSpan.FromMilliseconds(20));
            }

            if (item != null)
            {
                var st = item.Client;
                var writer = item.Writer;
                var cmd = item.CommandLine.Trim();

                try
                {
                    if (cmd.Equals("/list", StringComparison.OrdinalIgnoreCase))
                    {
                        if (st.Role != Role.Admin && st.Role != Role.ReadOnly) continue;
                        var files = Directory.GetFiles(STORAGE_DIR);
                        await writer.WriteLineAsync(string.Join("|", Array.ConvertAll(files, f => Path.GetFileName(f))));
                    }

                    else if (cmd.StartsWith("/read "))
                    {
                        string fn = cmd.Substring(6).Trim();
                        var path = Path.Combine(STORAGE_DIR, fn);
                        if (File.Exists(path))
                        {
                            foreach (var line in File.ReadLines(path))
                            {
                                await writer.WriteLineAsync(line);
                            }
                            await writer.WriteLineAsync("<<EOF>>");
                        }
                        else
                        {
                            await writer.WriteLineAsync("ERR:File not found");
                        }
                    }

                    else if (cmd.StartsWith("/upload "))
                    {
                        var parts = cmd.Split(' ', 3);
                        if (parts.Length < 3)
                        {
                            await writer.WriteLineAsync("ERR:Upload format: /upload <filename> <base64>");
                            continue;
                        }

                        var fname = parts[1];
                        var payload = parts[2];

                        try
                        {
                            var bytes = Convert.FromBase64String(payload);
                            var p = Path.Combine(STORAGE_DIR, fname);

                            await File.WriteAllBytesAsync(p, bytes);
                            await writer.WriteLineAsync("OK:Uploaded");

                            Interlocked.Add(ref totalBytesReceived, bytes.Length);
                            st.BytesReceived += bytes.Length;
                        }
                        catch
                        {
                            await writer.WriteLineAsync("ERR:Bad base64 payload");
                        }
                    }
                    else if (cmd.StartsWith("/download "))
                    {
                        if (st.Role != Role.Admin)
                        {
                            await writer.WriteLineAsync("ERR:Permission denied");
                            continue;
                        }

                        string fn = cmd.Substring(10).Trim();
                        var path = Path.Combine(STORAGE_DIR, fn);

                        if (!File.Exists(path))
                        {
                            await writer.WriteLineAsync("ERR:File not found");
                            continue;
                        }

                        byte[] bytes = await File.ReadAllBytesAsync(path);
                        string b64 = Convert.ToBase64String(bytes);

                        await writer.WriteLineAsync($"OK\t{fn}\t{b64}");

                        Interlocked.Add(ref totalBytesSent, bytes.Length);
                        st.BytesSent += bytes.Length;
                    }
                    else if (cmd.StartsWith("/delete "))
                    {
                        if (st.Role != Role.Admin) { await writer.WriteLineAsync("ERR:Permission denied"); continue; }

                        string fn = cmd.Substring(8).Trim();
                        var path = Path.Combine(STORAGE_DIR, fn);
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            await writer.WriteLineAsync("OK:Deleted");
                        }
                        else await writer.WriteLineAsync("ERR:File not found");
                    }
                    else if (cmd.StartsWith("/search "))
                    {
                        if (st.Role != Role.Admin) { await writer.WriteLineAsync("ERR:Permission denied"); continue; }

                        string kw = cmd.Substring(8).Trim();
                        var files = Directory.GetFiles(STORAGE_DIR);
                        var found = new List<string>();
                        foreach (var f in files)
                            if (Path.GetFileName(f).Contains(kw, StringComparison.OrdinalIgnoreCase))
                                found.Add(Path.GetFileName(f));
                        await writer.WriteLineAsync("SEARCHRESULT:" + string.Join("|", found));
                    }
                    else if (cmd.StartsWith("/info "))
                    {
                        if (st.Role != Role.Admin) { await writer.WriteLineAsync("ERR:Permission denied"); continue; }

                        string fn = cmd.Substring(6).Trim();
                        var path = Path.Combine(STORAGE_DIR, fn);
                        if (File.Exists(path))
                        {
                            var fi = new FileInfo(path);
                            await writer.WriteLineAsync($"INFO:Size={fi.Length};Created={fi.CreationTimeUtc:o};Modified={fi.LastWriteTimeUtc:o}");
                        }
                        else await writer.WriteLineAsync("ERR:File not found");
                    }
                }
                catch
                {
                    await writer.WriteLineAsync("ERR:Exception");
                }
            }
        }
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

    static async Task TrafficMonitorLoop()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("[REAL-TIME SERVER STATS]");
            Console.WriteLine(BuildStatsText());
            await Task.Delay(5000); 
        }
    }

    static async Task IdleScannerLoop()
    {
        while (true)
    {
        var now = DateTime.UtcNow;

        foreach (var kv in clients)
        {
            var s = kv.Value;
            if (s.IsConnected && (now - s.LastSeen).TotalSeconds > IDLE_TIMEOUT_SECONDS)
            {
                Console.WriteLine($"[SERVER] Idle timeout → disconnecting {s.Username}@{s.IP}");

                try { s.Tcp.Close(); } catch { }

                s.IsConnected = false;

            }
        }

        await Task.Delay(5000);
    }
    }


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
        public ClientState Client { get; set; }
        public string CommandLine { get; set; }
        public StreamWriter Writer { get; set; }

    }

}

