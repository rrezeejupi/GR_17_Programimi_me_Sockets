using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class ClientProgram
{
    static async Task Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: dotnet run -- <ip> <port> <username> <role>");
            return;
        }

        string serverIp = args[0];
        int serverPort = int.Parse(args[1]);
        string username = args[2];
        string role = args[3].ToLower();
        bool isAdmin = role == "admin";

        await RunClient(serverIp, serverPort, username, role, isAdmin);
    }

    static async Task RunClient(string ip, int port, string username, string role, bool isAdmin)
    {
        using TcpClient client = new TcpClient();
        await client.ConnectAsync(ip, port);
        using NetworkStream stream = client.GetStream();
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        try
        {
            await writer.WriteLineAsync($"HELLO {username} {role}");

            string response = await reader.ReadLineAsync();
            if (response == null)
            {
                Console.WriteLine("Server closed the connection (maybe busy).");
                return;
            }

            Console.WriteLine(response);
        }
        catch (IOException ex)
        {
            Console.WriteLine("Could not connect to server: " + ex.Message);
            return;
        }


        while (true)
        {
            Console.WriteLine("\n--- MENU ---");
            Console.WriteLine("1. List files");
            Console.WriteLine("2. Read file");
            if (isAdmin) Console.WriteLine("3. Upload file");
            if (isAdmin) Console.WriteLine("4. Download file");
            if (isAdmin) Console.WriteLine("5. Delete file");
            if (isAdmin) Console.WriteLine("6. Search files");
            if (isAdmin) Console.WriteLine("7. Info file");
            if (isAdmin) Console.WriteLine("8. Stats");
            Console.WriteLine("9. Exit");
            Console.Write("Choice: ");
            string choice = Console.ReadLine()!.Trim();

            if (choice == "9") break;

            var start = DateTime.UtcNow;
            var end = DateTime.UtcNow;

            switch (choice)
            {
                case "1":
                    await writer.WriteLineAsync("/list");
                    Console.WriteLine(await reader.ReadLineAsync());
                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;

                case "2":
                    Console.Write("Filename to read: ");
                    string fRead = Console.ReadLine()!;

                    await writer.WriteLineAsync($"/read {fRead}");

                    string line;
                    while ((line = await reader.ReadLineAsync()) != "<<EOF>>")
                    {
                        Console.WriteLine(line);
                    }

                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;

                case "3":
                    if (!isAdmin) { Console.WriteLine("Permission denied."); break; }

                    Console.Write("Filename to upload: ");
                    string fileName = Console.ReadLine()!.Trim();

                    string currentDir = Directory.GetCurrentDirectory();
                    string filePath = Path.Combine(currentDir, fileName);

                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"File not found: {filePath}");
                        break;
                    }

                    try
                    {
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        string b64Content = Convert.ToBase64String(fileBytes);


                        Console.WriteLine($"File size: {fileBytes.Length} bytes");
                        Console.WriteLine($"Base64 length: {b64Content.Length} chars");
                        Console.WriteLine($"Filename: '{fileName}'");


                        if (b64Content.Contains(' '))
                        {
                            Console.WriteLine("WARNING: Base64 contains spaces - this may break the command");
                        }


                        string uploadCommand = $"/upload {fileName} {b64Content}";
                        await writer.WriteLineAsync(uploadCommand);

                        string serverResp = await reader.ReadLineAsync();
                        Console.WriteLine($"Server: {serverResp}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }

                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;

                case "4":
                    if (!isAdmin)
                    {
                        Console.WriteLine("Permission denied.");
                        break;
                    }

                    Console.Write("Filename to download: ");
                    string downloadName = Console.ReadLine()!.Trim();

                    await writer.WriteLineAsync($"/download {downloadName}");

                    string downloadResp = await reader.ReadLineAsync();

                    if (downloadResp == null)
                    {
                        Console.WriteLine("ERR:No response from server");
                        break;
                    }

                    if (downloadResp.StartsWith("ERR"))
                    {
                        Console.WriteLine(downloadResp);
                        break;
                    }

                    var parts = downloadResp.Split(' ', 2);
                    if (parts.Length < 2 || parts[0] != "FILE")
                    {
                        Console.WriteLine("ERR:Bad server response - expected FILE format");
                        break;
                    }

                    string b64 = parts[1];
                    string serverFile = downloadName; 

                    try
                    {
                        byte[] fileBytes = Convert.FromBase64String(b64);

                        string savePath = Path.Combine(Directory.GetCurrentDirectory(), serverFile);
                        File.WriteAllBytes(savePath, fileBytes);

                        Console.WriteLine($"Downloaded and saved as: {savePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERR: " + ex.Message);
                    }

                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;
                case "5":
                    if (!isAdmin) { Console.WriteLine("Permission denied."); break; }
                    Console.Write("Filename to delete: ");
                    string fDelete = Console.ReadLine()!;
                    await writer.WriteLineAsync($"/delete {fDelete}");
                    Console.WriteLine(await reader.ReadLineAsync());
                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;

                case "6":
                    if (!isAdmin) { Console.WriteLine("Permission denied."); break; }
                    Console.Write("Search keyword: ");
                    string kw = Console.ReadLine()!;
                    await writer.WriteLineAsync($"/search {kw}");
                    Console.WriteLine(await reader.ReadLineAsync());
                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;
                case "7":
                    if (!isAdmin) { Console.WriteLine("Permission denied."); break; }
                    Console.Write("Filename for info: ");
                    string fInfo = Console.ReadLine()!;
                    await writer.WriteLineAsync($"/info {fInfo}");
                    Console.WriteLine(await reader.ReadLineAsync());
                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;
                case "8":
                    if (!isAdmin) { Console.WriteLine("Permission denied."); break; }
                    await writer.WriteLineAsync("/STATS");
                    Console.WriteLine(await reader.ReadLineAsync());
                    Console.WriteLine($"Response time: {(end - start).TotalMilliseconds} ms");
                    break;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
        }
        Console.WriteLine("Disconnected...");

    }


}