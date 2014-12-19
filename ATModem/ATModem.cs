using BrusDev.IO.Modems.Frames;
using BrusDev.IO.Modems.Parsers;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif
using System;
using System.Collections;
using System.Text;
using System.Threading;

namespace BrusDev.IO.Modems
{
    public abstract class ATModem: IDisposable
    {
        protected bool clientConnected;

        private ATFrame dataFrame;
        private long dataCount;
        private long dataLength;
        private bool dataDiscarded;
        private object dataSyncRoot;
        private AutoResetEvent dataReady;


        public event ATModemEventHandler ClientConnected;
        public event ATModemEventHandler ClientDisconnected;


        public string AccessPointName { get; set; }
        public string AccessUsername { get; set; }
        public string AccessPassword { get; set; }


        public ATModem()
        {
            this.clientConnected = false;

            this.dataFrame = null;
            this.dataCount = 0;
            this.dataLength = 0;
            this.dataDiscarded = false;
            this.dataSyncRoot = new object();
            this.dataReady = new AutoResetEvent(false);
        }

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
            if (!this.dataReady.WaitOne(timeout, true))
                throw new ATModemException(ATModemError.Timeout);

            lock (this.dataSyncRoot)
            {
                if (!this.clientConnected || this.dataDiscarded)
                {
                    this.dataReady.Set();

                    return 0;
                }

                count = dataFrame.DataStream.Read(buffer, index, count);

                //Verifico se sono andati persi dei dati della frame.
                if (count == 0 && this.dataCount < this.dataLength)
                {
                    this.dataDiscarded = true;
                }

                this.dataCount += count;

                //Verifico se la lettura dei dati della frame è incompleta.
                if (this.dataCount < this.dataLength)
                {
                    this.dataReady.Set();
                }

                return count;
            }
        }

        protected void SetDataFrame(ATFrame frame)
        {
            lock (this.dataSyncRoot)
            {
                if (this.dataCount < this.dataLength)
                {
                    this.dataDiscarded = true;
                    this.dataFrame = null;
                }
                else
                {
                    this.dataFrame = frame;
                    this.dataCount = 0;
                    this.dataLength = frame.DataStream.Length;
                }

                this.dataReady.Set();
            }
        }

        protected void OnClientConnected(EventArgs e)
        {
            lock (this.dataSyncRoot)
            {
                this.clientConnected = true;

                this.dataCount = 0;
                this.dataLength = 0;
                this.dataDiscarded = false;
                this.dataFrame = null;
                this.dataReady.Reset();

                if (this.ClientConnected != null)
                    this.ClientConnected(this, EventArgs.Empty);
            }
        }

        protected void OnClientDisconnected(EventArgs e)
        {
            lock (this.dataSyncRoot)
            {
                this.clientConnected = false;

                this.dataReady.Set();

                if (this.ClientDisconnected != null)
                    this.ClientDisconnected(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);

        ~ATModem()
        {
            Dispose(false);
        }
    }
}
