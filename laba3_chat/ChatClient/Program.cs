using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class ChatClient
{
    static Socket clientSocket;
    static string userName;
    static StringBuilder input = new StringBuilder();
    static object consoleLock = new object();

    static void Main()
    {
        Console.Write("Ваше имя: ");
        userName = Console.ReadLine();
        Console.Write("Введите ВАШ локальный IP: ");
        string myIp = Console.ReadLine();
        Console.Write("Введите ВАШ свободный порт: ");
        int myPort = int.Parse(Console.ReadLine());

        Console.Write("IP сервера: ");
        string serverIp = Console.ReadLine();
        Console.Write("Порт сервера: ");
        int serverPort = int.Parse(Console.ReadLine());
        Console.WriteLine();
        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(myIp), myPort);
            clientSocket.Bind(localEndPoint);

            // подключаемся к серверу
            clientSocket.Connect(new IPEndPoint(IPAddress.Parse(serverIp), serverPort));

            Console.WriteLine($"[ПОДКЛЮЧЕНО] Вы вошли как {userName} с адреса {clientSocket.LocalEndPoint}");

            SendMessage(2, userName);

            // Поток для приема сообщений 
            Thread receiveThread = new Thread(ReceiveMessages);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Console.Write("Вы: ");

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                lock (consoleLock)
                {
                    if (key.Key == ConsoleKey.Enter)
                    {
                        int currentLine = Console.CursorTop;
                        Console.SetCursorPosition(0, currentLine);
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.SetCursorPosition(0, currentLine);

                        string text = input.ToString();
                        input.Clear();

                        if (string.IsNullOrEmpty(text))
                        {
                            Console.Write("Вы: ");
                            continue;
                        }

                        if (text == "/exit") break;

                        SendMessage(1, text);

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {userName}: {text}");
                        Console.Write("Вы: ");
                    }
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (input.Length > 0)
                        {
                            input.Remove(input.Length - 1, 1);
                            Console.Write("\b \b");
                        }
                    }
                    else
                    {
                        input.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                }
            }
        }
        catch (Exception ex) { Console.WriteLine("Ошибка: " + ex.Message); }
        finally { clientSocket.Close(); }
    }

    static void SendMessage(byte type, string message)
    {
        byte[] content = Encoding.UTF8.GetBytes(message);
        byte[] packet = new byte[content.Length + 2];
        packet[0] = type;
        packet[1] = (byte)content.Length;
        Array.Copy(content, 0, packet, 2, content.Length);

        clientSocket.Send(packet);
    }

    static void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRead = clientSocket.Receive(buffer);
                if (bytesRead <= 0) break;

                byte type = buffer[0];
                int length = buffer[1];
                string content = Encoding.UTF8.GetString(buffer, 2, length);

                string time = DateTime.Now.ToString("HH:mm:ss");

                lock (consoleLock)
                {
                    int currentLine = Console.CursorTop;
                    Console.SetCursorPosition(0, currentLine);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, currentLine);

                    Console.WriteLine($"[{time}] {content}");

                    Console.Write("Вы: " + input.ToString());
                }
            }
        }
        catch { }
    }
}