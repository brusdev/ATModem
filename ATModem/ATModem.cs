using BrusDev.IO.Modems.Frames;
using BrusDev.IO.Modems.Parsers;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif
using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.IO;

namespace BrusDev.IO.Modems
{
    public abstract class ATModem: IDisposable
    {
        enum ATConnectionMode
        {
            Client,
            Server
        }

        class ATConnectionState
        {
            public bool ipConnectionOpened;
            public bool ipConnectionPending;
            public bool ipConnectionDataDiscarded;
            public ATConnectionMode ipConnectionMode;
            public AutoResetEvent ipConnectionDataReady;


            public ATConnectionState()
            {
                this.ipConnectionOpened = false;
                this.ipConnectionPending = false;
                this.ipConnectionDataDiscarded = false;
                this.ipConnectionMode = ATConnectionMode.Client;
                this.ipConnectionDataReady = new AutoResetEvent(false);
            }
        }


        private const int maximumConnections = 8;
        private const int maximumEnableTests = 8;

        private long ipConnectionId;
        private long ipConnectionDataCount;
        private long ipConnectionDataLength;
        private Stream ipConnectionDataStream;
        private object ipConnectionSyncRoot;

        private ATConnectionState[] ipConnectionState;

        private int ipConnectionServerCount;
        private AutoResetEvent ipConnectionServerReady;

        public string AccessPointName { get; set; }
        public string AccessUsername { get; set; }
        public string AccessPassword { get; set; }


        public ATModem()
        {
            this.ipConnectionId = -1;
            this.ipConnectionDataCount = 0;
            this.ipConnectionDataLength = 0;
            this.ipConnectionDataStream = null;
            this.ipConnectionSyncRoot = new object();
            this.ipConnectionState = new ATConnectionState[maximumConnections];
            for (int i = 0; i < maximumConnections; i++)
                this.ipConnectionState[i] = new ATConnectionState();
            
            this.ipConnectionServerCount = 0;
            this.ipConnectionServerReady = new AutoResetEvent(false);
        }

        protected virtual void Initialize()
        {
            bool deviceEnabled = false;

            //Resetto il dispositivo.
            try
            {
                this.Reset();
            }
            catch
            {
                Thread.Sleep(1000);
            }

            //Attendo l'abilitazione del dispositivo.
            for (int count = 0; count < maximumEnableTests; count++)
            {
                try
                {
                    this.Test();
                    deviceEnabled = true;
                    break;
                }
                catch
                {
                    Thread.Sleep(1000);
                }
            }

            if(!deviceEnabled)
                throw new ATModemException(ATModemError.Generic);
        }

        public abstract void Test();

        public abstract void Reset();

        public abstract void Close();

        public abstract void SetDNSSettings(string primaryIPAddress, string secondaryIPAddress);

        public abstract void OpenDataConnection();
        public abstract string GetLocalIPAddress();

        public abstract int OpenIPConnection(string mode, string ipAddress, int port);
        public abstract int OpenIPConnection(string mode, string ipAddress, int port, int timeout);

        public virtual void SendData(int id, byte[] buffer, int index, int count)
        {
            this.SendData(id, buffer, index, count, Timeout.Infinite);
        }

        public abstract void SendData(int id, byte[] buffer, int index, int count, int timeout);

        public abstract void CloseIPConnection(int id);

        public abstract void CloseDataConnection();

        public abstract string GetSerial();

        public abstract string GetIPConnectionStatus(int id);

        public abstract void StartListening(int port);

        public abstract void StopListening();

        public virtual int Accept()
        {
            ATConnectionState connectionState;

            this.ipConnectionServerReady.WaitOne();

            lock (this.ipConnectionSyncRoot)
            {
                for (int i = 0; i < maximumConnections; i++)
                {
                    connectionState = this.ipConnectionState[i];

                    if (connectionState.ipConnectionMode == ATConnectionMode.Server &&
                        connectionState.ipConnectionPending)
                    {
                        connectionState.ipConnectionPending = false;

                        this.ipConnectionServerCount--;

                        if(this.ipConnectionServerCount > 0)
                            this.ipConnectionServerReady.Set();
                        else
                            this.ipConnectionServerReady.Reset();

                        return i;
                    }
                }
            }

            throw new ATModemException(ATModemError.Generic);
        }

        public virtual int ReceiveData(int id, byte[] buffer, int index, int count)
        {
            return this.ReceiveData(id, buffer, index, count, Timeout.Infinite);
        }

        public virtual int ReceiveData(int id, byte[] buffer, int index, int count, int timeout)
        {
            ATConnectionState connectionState = this.ipConnectionState[id];

            if (!connectionState.ipConnectionDataReady.WaitOne(timeout, true))
                throw new ATModemException(ATModemError.Timeout);

            lock (this.ipConnectionSyncRoot)
            {
                if (!connectionState.ipConnectionOpened || connectionState.ipConnectionDataDiscarded)
                {
                    connectionState.ipConnectionDataReady.Set();

                    return 0;
                }

                count = ipConnectionDataStream.Read(buffer, index, count);

                //Verifico se sono andati persi dei dati della frame.
                if (count == 0 && this.ipConnectionDataCount < this.ipConnectionDataLength)
                {
                    connectionState.ipConnectionDataDiscarded = true;
                }

                this.ipConnectionDataCount += count;

                //Verifico se la lettura dei dati della frame è incompleta.
                if (this.ipConnectionDataCount < this.ipConnectionDataLength)
                {
                    connectionState.ipConnectionDataReady.Set();
                }

                return count;
            }
        }

        protected int GetConnectionId()
        {
            for (int i = 0; i < maximumConnections; i++)
                if (!this.ipConnectionState[i].ipConnectionOpened)
                    return i;

            throw new IndexOutOfRangeException();
        }

        protected void SetDataFrame(int id, ATFrame frame)
        {
            ATConnectionState connectionState = this.ipConnectionState[id];

            lock (this.ipConnectionSyncRoot)
            {
                if (this.ipConnectionDataCount < this.ipConnectionDataLength)
                {
                    ATConnectionState previousConnectionState = this.ipConnectionState[this.ipConnectionId];

                    //La connessione con ipConnectionId ha perso dei dati in ricezione.
                    previousConnectionState.ipConnectionDataDiscarded = true;
                    previousConnectionState.ipConnectionDataReady.Set();
                }

                if (connectionState.ipConnectionOpened && !connectionState.ipConnectionDataDiscarded)
                {
                    this.ipConnectionId = id;
                    this.ipConnectionDataCount = 0;
                    this.ipConnectionDataLength = frame.DataStream.Length;
                    this.ipConnectionDataStream = frame.DataStream;

                    connectionState.ipConnectionDataReady.Set();
                }
            }
        }

        protected void OnIPConnectionOpened(int id, bool remote)
        {
            lock (this.ipConnectionSyncRoot)
            {
                ATConnectionState connectionState = this.ipConnectionState[id];

                if (!connectionState.ipConnectionOpened)
                {
                    connectionState.ipConnectionOpened = true;
                    connectionState.ipConnectionPending = true;
                    connectionState.ipConnectionDataDiscarded = false;
                    connectionState.ipConnectionMode = ATConnectionMode.Client;
                    connectionState.ipConnectionDataReady.Reset();

                    if (remote)
                    {
                        connectionState.ipConnectionMode = ATConnectionMode.Server;

                        this.ipConnectionServerCount++;
                        this.ipConnectionServerReady.Set();
                    }
                }
            }
        }

        protected void OnIPConnectionClosed(int id)
        {
            lock (this.ipConnectionSyncRoot)
            {
                ATConnectionState connectionState = this.ipConnectionState[id];

                if (connectionState.ipConnectionOpened)
                {
                    connectionState.ipConnectionOpened = false;
                    connectionState.ipConnectionDataReady.Set();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
    }
}
