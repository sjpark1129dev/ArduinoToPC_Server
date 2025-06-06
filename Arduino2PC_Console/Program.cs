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
            Console.Write("연결할 시리얼 포트(COM 번호) 입력 (예: COM11): ");
            string portName = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(portName))
            {
                Console.WriteLine("포트명이 유효하지 않습니다. 프로그램을 종료합니다.");
                return;
            }

            // 2. TCP 포트 입력 받기
            Console.Write("사용할 TCP 포트 입력 (예: 8080): ");
            string tcpPortStr = Console.ReadLine()?.Trim();
            if (!int.TryParse(tcpPortStr, out int tcpPort) || tcpPort < 1 || tcpPort > 65535)
            {
                Console.WriteLine("TCP 포트 번호가 유효하지 않습니다. 프로그램을 종료합니다.");
                return;
            }

            //시리얼 포트 설정 코드
            serialPort = new SerialPort();
            serialPort.ReadTimeout = 2000;
            serialPort.PortName = portName;// 아두이노 인식 COM PORT (자신에 맞게 조정)
            serialPort.BaudRate = 115200;
            serialPort.DtrEnable = false; // false => true
            serialPort.NewLine = "\r\n"; // CRLF

            try
            {
                serialPort.Open();
                Console.WriteLine($"시리얼 포트 {portName} 연결됨.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[에러] 시리얼 포트를 열 수 없습니다: {ex.Message}");
                return;
            }

            // 4. TCP 서버 시작
            TcpListener server = new TcpListener(IPAddress.Any, tcpPort);
            server.Start();
            Console.WriteLine($"[TCP 서버] 포트 {tcpPort}에서 서버 시작됨.");

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