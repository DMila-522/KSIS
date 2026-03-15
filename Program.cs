using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace TracertLab
{
    class Program
    {
        const int ICMP_ECHO = 8;   
        const int ICMP_TIME_EXCEEDED = 11; 
        const int ICMP_ECHO_REPLY = 0; 

        static void Main(string[] args)
        {
            Console.Write("Введите IP-адрес или доменое имя: ");
            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return;

            try
            {
                IPAddress targetIP;

                if (IPAddress.TryParse(input, out targetIP))
                {
                    targetIP = IPAddress.Parse(input);
                    Console.WriteLine($"\nТрассировка маршрута к [{targetIP}]\n");
                }
                else
                {
                    IPHostEntry hostEntry = Dns.GetHostEntry(input);
                    targetIP = hostEntry.AddressList[0];
                    Console.WriteLine($"\nТрассировка маршрута к {input} [{targetIP}]\n");
                }

                // Запускаем трассировку
                RunTracert(targetIP);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            

            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
        }

        static void RunTracert(IPAddress destination)
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
            {
                socket.ReceiveTimeout = 3000;

                ushort processId = (ushort)Process.GetCurrentProcess().Id;

                ushort sequence = 0;

                for (int ttl = 1; ttl <= 30; ttl++)
                {
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);  // Установка TTL

                    Console.Write($"{ttl,2} ");

                    IPAddress hopAddress = null;
                    bool reachedDestination = false;

                    // Отправка 3 пакетов
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        sequence++;
                        
                        byte[] packet = CreateIcmpPacket(processId, sequence); // Создание ICMP пакета

                        Stopwatch sw = Stopwatch.StartNew();

                        try
                        {
                            socket.SendTo(packet, new IPEndPoint(destination, 0));

                            byte[] buffer = new byte[1024];
                            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                            int receivedBytes = socket.ReceiveFrom(buffer, ref remoteEndPoint);
                            sw.Stop();

                            IPAddress responderIP = ((IPEndPoint)remoteEndPoint).Address;  // IP ответившего узла
                            int ipHeaderLen = (buffer[0] & 0x0F) * 4;
                            byte icmpType = buffer[ipHeaderLen];

                            if (icmpType == ICMP_TIME_EXCEEDED || icmpType == ICMP_ECHO_REPLY)
                            {
                                if (icmpType == ICMP_ECHO_REPLY)
                                {
                                    // Проверяем ID в ответе
                                    ushort replyId = (ushort)((buffer[ipHeaderLen + 4] << 8) | buffer[ipHeaderLen + 5]);
                                    if (replyId != processId)
                                        continue; 
                                }

                                if (hopAddress == null)
                                    hopAddress = responderIP;

                                if (sw.ElapsedMilliseconds < 1)
                                    Console.Write("  <1 ms ");
                                else
                                    Console.Write($"{sw.ElapsedMilliseconds,4} ms ");

                                if (responderIP.Equals(destination))
                                    reachedDestination = true;
                            }
                            else
                            {
                                // Получен какой-то другой ICMP пакет 
                                attempt--;
                                sequence--; 
                                continue;
                            }
                        }
                        catch (SocketException)
                        {
                            Console.Write("   *    ");
                        }
                    }

                    // Выводим IP узла для этого шага
                    if (hopAddress != null)
                    {
                        Console.Write($" {hopAddress}");

                        try
                        {
                            string hostName = Dns.GetHostEntry(hopAddress).HostName;
                            if (hostName != hopAddress.ToString())
                                Console.Write($" [{hostName}]");
                        }
                        catch { }
                    }

                    Console.WriteLine(); 

                    if (reachedDestination)
                    {
                        Console.WriteLine("\nТрассировка завершена.");
                        break;
                    }
                }
            }
        }

        static byte[] CreateIcmpPacket(ushort id, ushort seq)
        {
            // Пакет: заголовок (8 байт) + данные (32 байта)
            byte[] packet = new byte[40];

            packet[0] = ICMP_ECHO;   
            packet[1] = 0;            

            packet[2] = 0;
            packet[3] = 0;

            packet[4] = (byte)((id >> 8) & 0xFF);
            packet[5] = (byte)(id & 0xFF);

            packet[6] = (byte)((seq >> 8) & 0xFF);
            packet[7] = (byte)(seq & 0xFF);

            for (int i = 8; i < packet.Length; i++)
                packet[i] = (byte)(i - 8);

            ushort checksum = CalculateChecksum(packet);
            packet[2] = (byte)((checksum >> 8) & 0xFF);
            packet[3] = (byte)(checksum & 0xFF);

            return packet;
        }

        static ushort CalculateChecksum(byte[] data)
        {
            long sum = 0;

            for (int i = 0; i < data.Length; i += 2)
            {
                ushort word;
                if (i + 1 < data.Length)
                    word = (ushort)((data[i] << 8) | data[i + 1]);
                else
                    word = (ushort)(data[i] << 8);

                sum += word;
            }

            while ((sum >> 16) != 0)
                sum = (sum & 0xFFFF) + (sum >> 16);

            return (ushort)(~sum & 0xFFFF);
        }
    }
}