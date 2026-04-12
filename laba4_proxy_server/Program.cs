using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProxyServer
{
    class Program
    {
        public static IPAddress ip = IPAddress.Parse("127.0.0.2");
        public static int port = 7700;

        static void Main(string[] args)
        {
            
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                serverSocket.Bind(new IPEndPoint(ip, port));
                serverSocket.Listen(10); 
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Прокси-сервер запущен на {ip}:{port}");
                Console.WriteLine("Ожидание запросов от браузера...\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Не удалось запустить сервер: {ex.Message}");
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
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss} Ошибка при приеме клиента: {ex.Message}");
                }
            }
        }

        static void HandleClient(Socket browserSocket)
        {
            Socket destinationSocket = null;
            try
            {
                byte[] buffer = new byte[4096]; 

                int bytesRead = browserSocket.Receive(buffer);
                if (bytesRead <= 0) return;

                string requestHeader = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                // Разбиение запрос на строки для анализа
                string[] lines = requestHeader.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0) return;

                string firstLine = lines[0]; 
                string[] requestParts = firstLine.Split(' ');

                if (requestParts.Length != 3) return;

                string method = requestParts[0];
                string fullUrl = requestParts[1];
                string httpVersion = requestParts[2];

                // Парсим URL для получения адреса конечного сервера
                Uri uri = new Uri(fullUrl);
                string host = uri.Host;
                int port = uri.Port;
                string pathAndQuery = uri.PathAndQuery;

                // Преобразование запроса (замена полного URL на относительный)
                string newFirstLine = $"{method} {pathAndQuery} {httpVersion}";
                string modifiedRequest = requestHeader.Replace(firstLine, newFirstLine);
                byte[] modifiedRequestBytes = Encoding.ASCII.GetBytes(modifiedRequest);

                destinationSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                destinationSocket.Connect(host, port);

                destinationSocket.Send(modifiedRequestBytes);

                // Чтение ответа от целевого сервера
                byte[] responseBuffer = new byte[4096];
                int respBytesRead = destinationSocket.Receive(responseBuffer);

                if (respBytesRead > 0)
                {
                    string responseHeader = Encoding.ASCII.GetString(responseBuffer, 0, respBytesRead);
                    string[] respLines = responseHeader.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string statusLine = respLines[0];

                    string statusCode = "Unknown";
                    int spaceIndex = statusLine.IndexOf(' ');
                    if (spaceIndex != -1)
                    {
                        statusCode = statusLine.Substring(spaceIndex + 1);
                    }
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {fullUrl} - {statusCode}");

                    browserSocket.Send(responseBuffer, respBytesRead, SocketFlags.None);

                    while (true)
                    {
                        respBytesRead = destinationSocket.Receive(responseBuffer);
                        if (respBytesRead <= 0) break;

                        browserSocket.Send(responseBuffer, respBytesRead, SocketFlags.None);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Соединение разорвано: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Произошла ОШИБКА: {ex.Message}");
            }
            finally
            {
                if (destinationSocket != null && destinationSocket.Connected)
                    destinationSocket.Close();
                if (browserSocket != null && browserSocket.Connected)
                    browserSocket.Close();
            }
        }
    }
}