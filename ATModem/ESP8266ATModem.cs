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
    public class ESP8266ATModem : ATModem
    {
        private bool disposed = false;

        private ATProtocol protocol;

        private int openingConnectionId;
        private int sendingConnectionId;
        private int closingConnectionId;
        private int checkingConnectionId;
        private string checkingConnectionStatus;
        private ManualResetEvent connectionEvent;
        private object connectionSyncRoot;
        private bool[] connectionStatus;
        private bool[] previousConnectionStatus;

        private string sendResult;
        private ManualResetEvent sendEvent;

        private ManualResetEvent sendPromptEvent;


        public ESP8266ATModem(Stream stream)
        {
            this.protocol = new ATProtocol(stream, ESP8266ATParser.GetInstance());
            this.protocol.EchoEnabled = false;
            this.protocol.FrameReceived += protocol_FrameReceived;

            this.openingConnectionId = -1;
            this.sendingConnectionId = -1;
            this.connectionEvent = new ManualResetEvent(false);
            this.sendEvent = new ManualResetEvent(false);
            this.sendPromptEvent = new ManualResetEvent(false);

            this.connectionSyncRoot = new object();
            this.connectionStatus = new bool[4];
            for (int i = 0; i < this.connectionStatus.Length; i++)
                this.connectionStatus[i] = false;
            this.previousConnectionStatus = new bool[4];
            for (int i = 0; i < this.previousConnectionStatus.Length; i++)
                this.previousConnectionStatus[i] = this.connectionStatus[i];

            this.Initialize();
        }

        private void protocol_FrameReceived(object sender, ATModemFrameEventArgs e)
        {
            ATFrame responseFrame = e.Frame;

            if (responseFrame.Command == ATCommand.CONNECT)
            {
                this.connectionEvent.Set();
            }
            else if (responseFrame.Command == ATCommand.SEND)
            {
                this.sendResult = responseFrame.Result;
                this.sendEvent.Set();
            }
            else if (responseFrame.Command == ATCommand.SEND_PROMPT)
            {
                this.sendPromptEvent.Set();
            }
            else if (responseFrame.Command == ATCommand.IPD)
            {
                this.SetDataFrame(Convert.ToInt32(responseFrame.OutParameters), responseFrame);
            }
            else if (responseFrame.Command == ATCommand.REMOTE_IP)
            {
                for (int i = 0; i < this.previousConnectionStatus.Length; i++)
                {
                    this.previousConnectionStatus[i] = this.connectionStatus[i];
                    this.connectionStatus[i] = false;
                }

                this.protocol.BeginProcess(this.protocol.CreateRequestFrame(
                    ATCommand.AT_CIPSTATUS, ATCommandType.Execution, null),
                    true, 300000, this.UpdateIpConnectionStatus, null);
            }
            else if (responseFrame.Command == ATCommand.CLOSED)
            {
                for (int i = 0; i < this.previousConnectionStatus.Length; i++)
                {
                    this.previousConnectionStatus[i] = this.connectionStatus[i];
                    this.connectionStatus[i] = false;
                }

                this.protocol.BeginProcess(this.protocol.CreateRequestFrame(
                    ATCommand.AT_CIPSTATUS, ATCommandType.Execution, null),
                    true, 300000, this.UpdateIpConnectionStatus, null);
            }
            else if (responseFrame.Command == ATCommand.AT_CIPSTATUS)
            {
                if (responseFrame.CommandType == ATCommandType.Execution)
                {

                }
                else if (responseFrame.CommandType == ATCommandType.Write)
                {
                    int connectionId = Convert.ToInt32(responseFrame.OutParameters);

                    if (this.checkingConnectionId == connectionId)
                    {
                        this.checkingConnectionStatus = responseFrame.Result;
                    }

                    this.connectionStatus[connectionId] = true;
                }
            }
        }

        private void UpdateIpConnectionStatus(IAsyncResult asyncResult)
        {
            lock (connectionSyncRoot)
            {
                for (int i = 0; i < this.previousConnectionStatus.Length; i++)
                {
                    if (this.connectionStatus[i] != this.previousConnectionStatus[i])
                    {
                        if (this.connectionStatus[i])
                        {
                            this.OnIPConnectionOpened(i, true);
                        }
                        else
                        {
                            this.OnIPConnectionClosed(i);
                        }
                    }
                }
            }
        }

        protected override void Initialize()
        {
            base.Initialize();

            //Disabilito l'echo dei comandi perch� inficia sulle prestazioni.
            this.SetEchoMode(false);

            //Disabilito eventuali connessioni precedenti.
            this.CloseDataConnection();

            //Imposto la modalit� di esecuzione.
            this.SetWIFIMode(3);

            //Disabilito la gestione delle connessioni multiple perch� al momento non � gestito.
            // In futuro si potrebbe gestire la modalit� multiconnessione.
            this.StartUpMultiIPConnection(true);
        }

        public override void Test()
        {
            ATFrame responseFrame = null;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void Close()
        {
            this.protocol.Close();
        }

        public void SetEchoMode(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(mode ? ATCommand.ATE1 : ATCommand.ATE0, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void OpenDataConnection()
        {
            string localIPAddress;

            this.JoinWIFIAccessPoint(this.AccessPointName, this.AccessPassword);

            localIPAddress = this.GetLocalIPAddress();
        }

        public override string GetLocalIPAddress()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIFSR, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result == "ERROR")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters.Trim('"');
        }

        public void SetConnectionMode(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPMODE, ATCommandType.Write, mode ? "1" : "0");

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void StartUpMultiIPConnection(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPMUX, ATCommandType.Write, mode ? "1" : "0");

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override int OpenIPConnection(string mode, string ipAddress, int port)
        {
            return this.OpenIPConnection(mode, ipAddress, port, Timeout.Infinite);
        }

        public override int OpenIPConnection(string mode, string ipAddress, int port, int timeout)
        {
            ATFrame responseFrame;
            ATFrame requestFrame;

            this.connectionEvent.Reset();

            lock (connectionSyncRoot)
            {
                this.openingConnectionId = GetConnectionId();

                requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSTART, ATCommandType.Write,
                    String.Concat(this.openingConnectionId, ",\"", mode, "\",\"", ipAddress, "\",", port.ToString()));
                responseFrame = (ATFrame)this.protocol.Process(requestFrame, true, 6000);

                if (responseFrame.Result != "OK")
                    throw new ATModemException(ATModemError.Generic);

                if (!this.connectionEvent.WaitOne(timeout, true))
                    throw new ATModemException(ATModemError.Timeout);

                this.connectionStatus[this.openingConnectionId] = true;

                this.OnIPConnectionOpened(this.openingConnectionId, false);
            }

            return this.openingConnectionId;
        }

        public override void SendData(int id, byte[] buffer, int index, int count, int timeout)
        {
            ATFrame requestFrame;


            lock (this.protocol.SyncRoot)
            {
                this.sendingConnectionId = id;

                requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSEND, ATCommandType.Write, String.Concat(this.sendingConnectionId.ToString(), ",", count));


                this.sendEvent.Reset();
                this.sendPromptEvent.Reset();

                this.protocol.Process(requestFrame);

                requestFrame.DataStream.Write(buffer, index, count);

                if (!this.sendEvent.WaitOne(timeout, true))
                    throw new ATModemException(ATModemError.Timeout);

                if (this.sendResult != "OK")
                    throw new ATModemException(ATModemError.Generic);
            }
        }

        public override void CloseIPConnection(int id)
        {
            lock (this.connectionSyncRoot)
            {
                ATFrame responseFrame;
                ATFrame requestFrame;

                this.closingConnectionId = id;

                requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPCLOSE, ATCommandType.Write, this.closingConnectionId.ToString());

                try
                {
                    responseFrame = (ATFrame)this.protocol.Process(requestFrame);

                    if (responseFrame.Result != "OK")
                        throw new ATModemException(ATModemError.Generic);
                }
                catch (ATModemException)
                {
                    if (this.GetIPConnectionStatus(id) != null)
                        throw new ATModemException(ATModemError.Generic);
                }

                this.connectionStatus[id] = false;

                this.OnIPConnectionClosed(id);
            }
        }

        public override void CloseDataConnection()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CWQAP, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result == "ERROR")
                throw new ATModemException(ATModemError.Generic);
        }

        public override string GetIPConnectionStatus(int id)
        {
            this.checkingConnectionId = id;
            this.checkingConnectionStatus = null;

            this.GetIPConnectionStatus();

            return this.checkingConnectionStatus;
        }

        private string GetIPConnectionStatus()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSTATUS, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }


        public override void StartListening(int port)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSERVER, ATCommandType.Write, String.Concat("1,", port));

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void StopListening()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSERVER, ATCommandType.Write, "0");

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.protocol.Close();
                }

                disposed = true;
            }
        }


        /***ESP8266***/
        public override void Reset()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_RST, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame, true, 10000);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void GetFirmwareVersion()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_GMR, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame, true, 10000);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void SetWIFIMode(int mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CWMODE, ATCommandType.Write, mode.ToString());

            responseFrame = (ATFrame)this.protocol.Process(requestFrame, true, 10000);

            if (responseFrame.Result == "ERROR")
                throw new ATModemException(ATModemError.Generic);
        }

        private void JoinWIFIAccessPoint(string ssid,string password)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CWJAP, ATCommandType.Write, String.Concat("\"", ssid, "\",\"", password, "\""));

            responseFrame = (ATFrame)this.protocol.Process(requestFrame, true, 10000);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void SetDNSSettings(string primaryIPAddress, string secondaryIPAddress)
        {
            throw new NotImplementedException();
        }

        public override string GetSerial()
        {
            throw new NotImplementedException();
        }
    }
}
