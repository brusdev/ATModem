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

namespace Tester.ESP8266
{
    class Program
    {
        static void Main(string[] args)
        {
            //string buffer = "STATUS:3\r\n+CIPSTATUS:0,\"TCP\",\"74.125.133.94\",80,0\r\n+CIPSTATUS:1,\"TCP\",\"192.168.20.1\",80,0\r\n\r\nOK\r\n";
            string buffer = "STATUS:3\r\n\r\nOK\r\n";
            BrusDev.Text.RegularExpressions.Regex r = new BrusDev.Text.RegularExpressions.Regex(@"STATUS:[0-9]\r\n(\+CIPSTATUS:[^\r\n]+\r\n)*\r\n(OK)\r\n");
            BrusDev.Text.RegularExpressions.Match m = r.Match(buffer);

            int connectionId;
            int clientConnectionId;
            int receivedBytes;
            int totalReceivedBytes;
            byte[] sendingBuffer;
            byte[] receivingBuffer;
            string remoteIPAddress;
            string connectionStatus;
#if MF_FRAMEWORK
            Microsoft.SPOT.Hardware.OutputPort deviceSwitch = new Microsoft.SPOT.Hardware.OutputPort(
                SecretLabs.NETMF.Hardware.NetduinoPlus.Pins.GPIO_PIN_D2, false);
#endif

            receivingBuffer = new byte[128];


            using (SerialPort serialPort = new SerialPort("COM10", 9600,
                System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One))
            {
                serialPort.Open();

#if MF_FRAMEWORK
                deviceSwitch.Write(true);

                using (ESP8266ATModem modem = new ESP8266ATModem(serialPort))
#else
                using (ESP8266ATModem modem = new ESP8266ATModem(serialPort.BaseStream))
#endif
                {
                    //modem.AccessPointName = "Vodafone-brusnet";
                    //modem.AccessPassword = "dommiccargia";
                    modem.AccessPointName = "LeonardoRicerche";
                    modem.AccessPassword = "LeonardoRicercheSrl2014AD";

                    modem.OpenDataConnection();

                    //remoteIPAddress = "192.168.10.1";
                    remoteIPAddress = "www.google.it";

                    connectionStatus = modem.GetIPConnectionStatus(0);

                    modem.StartListening(1234);

                    clientConnectionId = modem.Accept();

                    sendingBuffer = Encoding.UTF8.GetBytes("HELLO\r\n");
                    modem.SendData(clientConnectionId, sendingBuffer, 0, sendingBuffer.Length);

                    totalReceivedBytes = 0;
                    while ((receivedBytes = modem.ReceiveData(clientConnectionId, receivingBuffer, 0, receivingBuffer.Length)) > 0)
                    {
                        totalReceivedBytes += receivedBytes;

                        //Debug.Print(new string(Encoding.UTF8.GetChars(receivingBuffer, 0, receivedBytes)));

                        //modem.SendData(clientConnectionId, receivingBuffer, 0, receivedBytes);

                        Debug.Print("ReceiveData > " + receivedBytes + "/" + totalReceivedBytes);
                    }

                    connectionId = modem.OpenIPConnection("TCP", remoteIPAddress, 80);

                    connectionStatus = modem.GetIPConnectionStatus(connectionId);


                    sendingBuffer = Encoding.UTF8.GetBytes("GET / HTTP/1.0\r\n\r\n");
                    modem.SendData(connectionId, sendingBuffer, 0, sendingBuffer.Length);

                    totalReceivedBytes = 0;
                    while ((receivedBytes = modem.ReceiveData(connectionId, receivingBuffer, 0, receivingBuffer.Length)) > 0)
                    {
                        totalReceivedBytes += receivedBytes;

                        if (totalReceivedBytes < 1000)
                        {
                            Debug.Print(new string(Encoding.UTF8.GetChars(receivingBuffer, 0, receivedBytes)));
                        }

                        //modem.SendData(clientConnectionId, receivingBuffer, 0, receivedBytes);

                        Debug.Print("ReceiveData > " + receivedBytes + "/" + totalReceivedBytes);
                    }

                    modem.CloseIPConnection(connectionId);
                    modem.CloseDataConnection();
                }
            }
        }
    }
}
