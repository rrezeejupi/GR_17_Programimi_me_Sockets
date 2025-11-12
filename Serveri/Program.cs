using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class TCPServer
{
    static void Main(string[] args)
    {
        TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 6789);
        listener.Start();
        Console.WriteLine("Serveri po degjon ne portin 6789...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Console.WriteLine("Klient i ri u lidh!");
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine("Mesazhi nga klienti: " + message);

            string response = "Echo: " + message;
            byte[] responseData = Encoding.UTF8.GetBytes(response);
            stream.Write(responseData, 0, responseData.Length);

            client.Close();
        }
    }
}
