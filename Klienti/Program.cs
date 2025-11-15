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

        await writer.WriteLineAsync($"HELLO {username} {role}");
        Console.WriteLine(await reader.ReadLineAsync());

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

            switch (choice)
            {
                case "1":
                    await writer.WriteLineAsync("/list");
                    Console.WriteLine(await reader.ReadLineAsync());
                    break;
                case "2":
                    break;
                case "3": // Upload-i i fileve
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
                    break;
                case "4":
                    break;
                case "5":
                    if (!isAdmin) { Console.WriteLine("Permission denied."); break; }
                    Console.Write("Filename to delete: ");
                    string fDelete = Console.ReadLine()!;
                    await writer.WriteLineAsync($"/delete {fDelete}");
                    Console.WriteLine(await reader.ReadLineAsync());
                    break;
                case "6":
                    break;
                case "7":
                    break;
                case "8":
                    break;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
        }
        Console.WriteLine("Disconnected...");

    }


}