using System;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Arduino2PC_Console_3
{
    class Program
    {
        private static SerialPort serialPort;
        static object serialLock = new object();
        static void Main(string[] args)
        {
            //시리얼 포트 설정 코드
            serialPort = new SerialPort();
            serialPort.ReadTimeout = 2000;
            serialPort.PortName = "COM11";// 아두이노 인식 COM PORT (자신에 맞게 조정)
            serialPort.BaudRate = 115200;
            serialPort.DtrEnable = false; // false => true
            serialPort.NewLine = "\r\n"; // CRLF
            serialPort.Open();
            Console.WriteLine("시리얼 포트 연결됨.");


            //// 시리얼 포트 점검 코드
            //while (true)
            //{
            //    Console.Write("\n입력 > ");
            //    string input = Console.ReadLine()?.Trim();

            //    if (string.IsNullOrEmpty(input)) continue;
            //    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            //    serialPort.DiscardInBuffer();
            //    serialPort.WriteLine(input); // 아두이노에 명령 전송

            //    try
            //    {
            //        string response = serialPort.ReadLine(); // 응답 수신
            //        Console.WriteLine("[아두이노 응답] " + response);
            //    }
            //    catch (TimeoutException)
            //    {
            //        Console.WriteLine("[에러] 아두이노 응답 없음 (타임아웃)");
            //    }
            //}

            //TCP 서버 코드
            TcpListener server = new TcpListener(IPAddress.Any, 8080);
            server.Start();
            Console.WriteLine("TCP 서버 시작됨 (포트 8080)");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("클라이언트 연결됨");

                // 각각의 클라이언트를 스레드에서 처리
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
        }

        static void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];

                while (client.Connected)
                {
                    int readBytes = stream.Read(buffer, 0, buffer.Length);
                    if (readBytes == 0) break; // 클라이언트가 연결을 종료한 경우

                    string command = Encoding.UTF8.GetString(buffer, 0, readBytes).Trim();
                    Console.WriteLine($"[클라이언트 명령] {command}");

                    string response = "NO_RESPONSE";

                    lock (serialLock)
                    {
                        serialPort.DiscardInBuffer();
                        serialPort.WriteLine(command);

                        try
                        {
                            response = serialPort.ReadLine();
                        }
                        catch (TimeoutException)
                        {
                            response = "TIMEOUT";
                        }
                    }

                    byte[] respBytes = Encoding.UTF8.GetBytes(response + "\n");
                    stream.Write(respBytes, 0, respBytes.Length);
                    Console.WriteLine($"[응답 전송] {response}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[에러] " + ex.Message);
            }
            finally
            {
                client.Close();
                Console.WriteLine("클라이언트 연결 종료");
            }
        }
    }
}