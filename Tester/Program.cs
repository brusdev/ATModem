#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif
using System;
using System.Text;
using System.Threading;
using BrusDev.IO.Modems;
using System.Diagnostics;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            ATModem modem;
            int receivedBytes;
            byte[] sendingBuffer;
            byte[] receivingBuffer;
            string remoteIPAddress;
            StringBuilder receivedStringBuilder;


            receivingBuffer = new byte[128];
            receivedStringBuilder = new StringBuilder();

            modem = new SIM900ATModem();
            modem.Open("COM1", 19200, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One, System.IO.Ports.Handshake.None);
            modem.AccessPointName = "web.omnitel.it";
            modem.ClientConnected += modem_ClientConnected;
            modem.ClientDisconnected += modem_ClientDisconnected;
            modem.DataReceived += modem_DataReceived;


            modem.OpenDataConnection();

            remoteIPAddress = modem.QueryDNSIPAddress("punto-informatico.it");

            modem.ConnectIPClient("TCP", remoteIPAddress, 80);


            sendingBuffer = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: punto-informatico.it\r\n\r\n");
            modem.SendData(sendingBuffer, 0, sendingBuffer.Length);

            while ((receivedBytes = modem.ReceiveData(receivingBuffer, 0, receivingBuffer.Length)) > 0)
            {
                receivedStringBuilder.Append(Encoding.UTF8.GetChars(receivingBuffer, 0, receivedBytes));
            }

            modem.DisconnectIPClient();
            modem.CloseDataConnection();
        }

        static void modem_ClientConnected(object sender, EventArgs e)
        {
            Debug.Print("ClientConnected");
        }

        static void modem_DataReceived(object sender, ATModemDataEventArgs e)
        {
            Debug.Print("DataReceived >");
            Debug.Print(new string(Encoding.UTF8.GetChars(e.Data, 0, e.Data.Length)));
        }

        static void modem_ClientDisconnected(object sender, EventArgs e)
        {
            Debug.Print("ClientDisconnected");
        }
    }
}
