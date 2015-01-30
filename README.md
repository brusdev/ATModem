ATModem C#
=======

Library to control a modem with AT commands: SIM900 and ESP8266.

Getting Started
---------------

### Untyped Documents
```C#
using BrusDev.IO.Modems;
```

```C#
int connectionId;
int receivedBytes;
int totalReceivedBytes;
byte[] sendingBuffer;
byte[] receivingBuffer;
string connectionStatus;


using (SerialPort serialPort = new SerialPort("COM10", 9600,
	System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One))
{
	serialPort.Open();

#if MF_FRAMEWORK
	using (ESP8266ATModem modem = new ESP8266ATModem(serialPort))
#else
	using (ESP8266ATModem modem = new ESP8266ATModem(serialPort.BaseStream))
#endif
	{

		modem.OpenDataConnection();
		connectionId = modem.OpenIPConnection("TCP", "www.google.it", 80);

		connectionStatus = modem.GetIPConnectionStatus(connectionId);


		sendingBuffer = Encoding.UTF8.GetBytes("GET / HTTP/1.0\r\n\r\n");
		modem.SendData(connectionId, sendingBuffer, 0, sendingBuffer.Length);

		totalReceivedBytes = 0;
		while ((receivedBytes = modem.ReceiveData(connectionId, receivingBuffer, 0, receivingBuffer.Length)) > 0)
		{
			totalReceivedBytes += receivedBytes;

			Debug.Print("ReceiveData > " + receivedBytes + "/" + totalReceivedBytes);
		}

		modem.CloseIPConnection(connectionId);
		modem.CloseDataConnection();
	}
}
```
