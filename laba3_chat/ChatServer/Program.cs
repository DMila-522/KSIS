using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;

class ChatServer
{
    static Dictionary<Socket, string> clientNames = new Dictionary<Socket, string>();

    static void Main()
    {
        Console.Write("Введите IP сервера: ");
        string ip = Console.ReadLine();
        Console.Write("Введите порт: ");
        if (!int.TryParse(Console.ReadLine(), out int port))
        {
            Console.WriteLine("Ошибка: Некорректный формат порта.");
            return;
        }

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            serverSocket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
            serverSocket.Listen(10);
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [СИСТЕМА] Сервер успешно запущен на {ip}:{port}");
        }
        catch (SocketException ex)
        {
            // Код ошибки 10048 означает "Порт уже используется"
            if (ex.ErrorCode == 10048)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ОШИБКА] Порт {port} уже занят другим приложением!");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ОШИБКА] Не удалось запустить сервер: {ex.Message}");
            }
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ОШИБКА] {ex.Message}");
            return;
        }

        while (true)
        {
            try
            {
                Socket client = serverSocket.Accept();
                Thread thread = new Thread(() => HandleClient(client));
                thread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ОШИБКА] Ошибка при приеме клиента: {ex.Message}");
            }
        }
    }

    static void HandleClient(Socket client)
    {
        IPEndPoint remoteIp = client.RemoteEndPoint as IPEndPoint;
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRead = client.Receive(buffer);
                if (bytesRead <= 0) break;

                byte type = buffer[0];
                int length = buffer[1];
                string content = Encoding.UTF8.GetString(buffer, 2, length);

                if (type == 2) // Тип 2: Регистрация имени
                {
                    clientNames[client] = content;
                    Console.WriteLine($" {DateTime.Now:HH:mm:ss} [ПОДКЛЮЧЕНИЕ] {content} ({remoteIp.Address}:{remoteIp.Port})");
                    Broadcast(3, $"{content} теперь в чате!", client); // Тип 3: Системное уведомление
                }
                else if (type == 1) // Тип 1: Обычное сообщение
                {
                    Broadcast(1, $"{clientNames[client]}: {content}", client);
                }
            }
        }
        catch { }
        finally
        {
            if (clientNames.ContainsKey(client))
            {
                string name = clientNames[client];
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ОТКЛЮЧЕНИЕ] {name} ({remoteIp.Address}:{remoteIp.Port})");
                Broadcast(3, $"{name} покинул чат!", client);
                clientNames.Remove(client);
            }
            client.Close();
        }
    }

    // Рассылка всем
    static void Broadcast(byte type, string message, Socket sender)
    {
        byte[] content = Encoding.UTF8.GetBytes(message);
        byte[] packet = new byte[content.Length + 2];
        packet[0] = type;
        packet[1] = (byte)content.Length;
        Array.Copy(content, 0, packet, 2, content.Length);

        lock (clientNames)
        {
            foreach (var client in clientNames.Keys)
            {
                if (client != sender) client.Send(packet);
            }
        }
    }
}