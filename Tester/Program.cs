#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif
using System;
using System.Text;
using System.Threading;
using BrusDev.IO.Modems;
using System.Diagnostics;
using System.IO.Ports;
using System.IO;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            int receivedBytes;
            int totalReceivedBytes;
            byte[] sendingBuffer;
            byte[] receivingBuffer;
            string remoteIPAddress;


            receivingBuffer = new byte[128];

            using (SerialPort serialPort = new SerialPort("COM1", 19200,
                System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One))
            {
                serialPort.Open();
#if MF_FRAMEWORK
                using (ATModem modem = new SIM900ATModem(serialPort))
#else
                using (ATModem modem = new SIM900ATModem(serialPort.BaseStream))
#endif
                {
                    modem.AccessPointName = "web.omnitel.it";
                    //modem.AccessPointName = "internet.wind";
                    modem.ClientConnected += modem_ClientConnected;
                    modem.ClientDisconnected += modem_ClientDisconnected;


                    modem.OpenDataConnection();

                    remoteIPAddress = modem.QueryDNSIPAddress("punto-informatico.it");
                    //remoteIPAddress = modem.QueryDNSIPAddress("www.google.it");

                    modem.ConnectIPClient("TCP", remoteIPAddress, 80);


                    sendingBuffer = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: punto-informatico.it\r\n\r\n");
                    //sendingBuffer = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.google.it\r\n\r\n");
                    modem.SendData(sendingBuffer, 0, sendingBuffer.Length);

                    totalReceivedBytes = 0;
                    while ((receivedBytes = modem.ReceiveData(receivingBuffer, 0, receivingBuffer.Length)) > 0)
                    {
                        totalReceivedBytes += receivedBytes;
                        Debug.Print("ReceiveData > " + receivedBytes + "/" + totalReceivedBytes);

                        if (totalReceivedBytes < 1000)
                        {
                            Debug.Print(new string(Encoding.UTF8.GetChars(receivingBuffer, 0, receivedBytes)));
                        }

                        Thread.Sleep(1000);
                    }

                    modem.DisconnectIPClient();
                    modem.CloseDataConnection();
                }
            }
        }

        static void modem_ClientConnected(object sender, EventArgs e)
        {
            Debug.Print("ClientConnected");
        }

        static void modem_ClientDisconnected(object sender, EventArgs e)
        {
            Debug.Print("ClientDisconnected");
        }
    }
}
