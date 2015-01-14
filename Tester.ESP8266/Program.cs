﻿#if MF_FRAMEWORK
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
            int connectionId;
            int receivedBytes;
            int totalReceivedBytes;
            byte[] sendingBuffer;
            byte[] receivingBuffer;
            string remoteIPAddress;
#if MF_FRAMEWORK
            Microsoft.SPOT.Hardware.OutputPort deviceSwitch = new Microsoft.SPOT.Hardware.OutputPort(
                SecretLabs.NETMF.Hardware.NetduinoPlus.Pins.GPIO_PIN_D2, false);
#endif

            receivingBuffer = new byte[128];


            using (SerialPort serialPort = new SerialPort("COM1", 9600,
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

                    connectionId = modem.OpenIPConnection("TCP", remoteIPAddress, 80);


                    sendingBuffer = Encoding.UTF8.GetBytes("GET / HTTP/1.0\r\n\r\n");
                    modem.SendData(connectionId, sendingBuffer, 0, sendingBuffer.Length);

                    totalReceivedBytes = 0;
                    while ((receivedBytes = modem.ReceiveData(connectionId, receivingBuffer, 0, receivingBuffer.Length)) > 0)
                    {
                        totalReceivedBytes += receivedBytes;
                        Debug.Print("ReceiveData > " + receivedBytes + "/" + totalReceivedBytes);

                        if (totalReceivedBytes < 1000)
                        {
                            Debug.Print(new string(Encoding.UTF8.GetChars(receivingBuffer, 0, receivedBytes)));
                        }
                    }

                    modem.CloseIPConnection(connectionId);
                    modem.CloseDataConnection();
                }
            }
        }
    }
}
