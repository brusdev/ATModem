using BrusDev.IO.Modems.Frames;
using BrusDev.IO.Modems.Parsers;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif
using System;
using System.Collections;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace BrusDev.IO.Modems
{
    public abstract class ATModem
    {
        protected bool clientConnected;

        private int dataToReceive;
        private ArrayList receivedDataBytesList;
        private ArrayList receivedDataIndexesList;
        private ManualResetEvent receivedDataReady;


        public event ATModemEventHandler ClientConnected;
        public event ATModemEventHandler ClientDisconnected;
        public event ATModemDataEventHandler DataReceived;


        public int DataToReceive { get { lock (this.receivedDataBytesList) { return this.dataToReceive; } } }
        
        public string AccessPointName { get; set; }
        public string AccessUsername { get; set; }
        public string AccessPassword { get; set; }


        public ATModem()
        {
            this.clientConnected = false;
            this.receivedDataBytesList = new ArrayList();
            this.receivedDataIndexesList = new ArrayList();
            this.receivedDataReady = new ManualResetEvent(false);

        }

        public abstract void Open(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake);

        public abstract void Close();

        public abstract void SetDNSSettings(string primaryIPAddress, string secondaryIPAddress);

        public abstract string QueryDNSIPAddress(string domaninName);
        public abstract string QueryDNSIPAddress(string domainName, int timeout);

        public abstract void OpenDataConnection();
        public abstract string GetLocalIPAddress();

        public abstract void ConnectIPClient(string mode, string ipAddress, int port);
        public abstract void ConnectIPClient(string mode, string ipAddress, int port, int timeout);

        public abstract void SendData(byte[] buffer, int index, int count);

        public abstract void SendData(byte[] buffer, int index, int count, int timeout);

        public abstract void DisconnectIPClient();

        public abstract void CloseDataConnection();

        public abstract string GetPinRequired();

        public abstract string GetSignalQualityReport();

        public abstract string GetGsmNetworkRegistration();

        public abstract string GetGprsNetworkRegistration();

        public abstract string GetGPRSServiceState();

        public abstract string GetImei();

        public abstract string GetConnectionStatus();

        public virtual int ReceiveData(byte[] buffer, int index, int count)
        {
            return this.ReceiveData(buffer, index, count, Timeout.Infinite);
        }

        public virtual int ReceiveData(byte[] buffer, int index, int count, int timeout)
        {
            if (!this.receivedDataReady.WaitOne(timeout, true))
                throw new ATModemException(ATModemError.Timeout);


            lock (this.receivedDataBytesList)
            {
                if(this.receivedDataBytesList.Count > 0)
                {
                    byte[] receivedDataByte = (byte[])this.receivedDataBytesList[0];
                    int receivedDataIndex = (int)this.receivedDataIndexesList[0];
                    int receivedDataCount = receivedDataByte.Length - receivedDataIndex;

                    if (count > receivedDataCount)
                        count = receivedDataCount;

                    Array.Copy(receivedDataByte, receivedDataIndex, buffer, index, count);

                    receivedDataIndex += count;

                    if (receivedDataIndex == receivedDataByte.Length)
                    {
                        this.receivedDataBytesList.RemoveAt(0);
                        this.receivedDataIndexesList.RemoveAt(0);

                        if (this.clientConnected && this.receivedDataBytesList.Count == 0)
                            this.receivedDataReady.Reset();
                    }
                    else
                    {
                        this.receivedDataIndexesList[0] = receivedDataIndex;
                    }

                    return count;
                }
            }

            return 0;
        }

        protected void AddReceivedData(byte[] data)
        {
            lock (this.receivedDataBytesList)
            {
                if (data.Length > 0)
                {
                    this.dataToReceive += data.Length;
                    this.receivedDataBytesList.Add(data);
                    this.receivedDataIndexesList.Add(0);

                    if (this.receivedDataBytesList.Count > 0)
                        this.receivedDataReady.Set();

                    if (this.DataReceived != null)
                        this.DataReceived(this, new ATModemDataEventArgs(data));
                }
            }
        }

        protected void OnClientConnected(EventArgs e)
        {
            lock (this.receivedDataBytesList)
            {
                this.clientConnected = true;
                this.receivedDataReady.Reset();

                if (this.ClientConnected != null)
                    this.ClientConnected(this, EventArgs.Empty);
            }
        }

        protected void OnClientDisconnected(EventArgs e)
        {
            lock (this.receivedDataBytesList)
            {
                this.clientConnected = false;

                this.receivedDataBytesList.Clear();
                this.receivedDataIndexesList.Clear();

                this.receivedDataReady.Set();

                if (this.ClientDisconnected != null)
                    this.ClientDisconnected(this, EventArgs.Empty);
            }
        }
    }
}
