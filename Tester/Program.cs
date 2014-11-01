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
            byte[] receivingBuffer;
            string remoteIPAddress;
            StringBuilder receivedStringBuilder;


            receivingBuffer = new byte[128];
            receivedStringBuilder = new StringBuilder();

            modem = new SIM900ATModem();
            modem.Open("COM1", 19200, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One, System.IO.Ports.Handshake.None);
            modem.AccessPointName = "internet.wind";
            modem.ClientConnected += modem_ClientConnected;
            modem.ClientDisconnected += modem_ClientDisconnected;
            modem.DataReceived += modem_DataReceived;


            modem.OpenDataConnection();

            remoteIPAddress = modem.QueryDNSIPAddress("www.microsoft.it");

            modem.ConnectIPClient("TCP", remoteIPAddress, 80);

            modem.SendData(new byte[] { (byte)'1', (byte)'2', (byte)'3', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' }, 0, 7);

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
