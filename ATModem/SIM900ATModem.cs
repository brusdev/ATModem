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
    public class SIM900ATModem : ATModem
    {
        private bool disposed = false;

        private ATProtocol protocol;

        private ManualResetEvent connectionEvent;

        private string dnsQueryParameters;
        private ManualResetEvent dnsQueryEvent;

        private string sendResult;
        private ManualResetEvent sendEvent;

        private ManualResetEvent sendPromptEvent;

        private object connectionSyncRoot;


        public SIM900ATModem(Stream stream)
        {
            this.protocol = new ATProtocol(stream, SIM900ATParser.GetInstance());
            this.protocol.FrameReceived += protocol_FrameReceived;

            this.connectionEvent = new ManualResetEvent(false);
            this.dnsQueryEvent = new ManualResetEvent(false);
            this.sendEvent = new ManualResetEvent(false);
            this.sendPromptEvent = new ManualResetEvent(false);

            this.connectionSyncRoot = new object();

            this.Initialize();
        }

        private void protocol_FrameReceived(object sender, ATModemFrameEventArgs e)
        {
            ATFrame responseFrame = e.Frame;

            if (responseFrame.Command == ATCommand.CONNECT)
            {
                this.connectionEvent.Set();
            }
            else if (responseFrame.Command == ATCommand.CDNSGIP)
            {
                this.dnsQueryParameters = responseFrame.OutParameters;
                this.dnsQueryEvent.Set();
            }
            else if (responseFrame.Command == ATCommand.SEND)
            {
                this.sendResult = responseFrame.Result;
                this.sendEvent.Set();
            }
            else if (responseFrame.Command == ATCommand.CLOSED)
            {
                lock (connectionSyncRoot)
                {
                    this.OnClientDisconnected(EventArgs.Empty);
                }
            }
            else if (responseFrame.Command == ATCommand.NORMAL_POWER_DOWN)
            {
                this.Initialize();
            }
            else if (responseFrame.Command == ATCommand.SEND_PROMPT)
            {
                this.sendPromptEvent.Set();
            }
            else if (responseFrame.Command == ATCommand.IPD)
            {
                this.SetDataFrame(responseFrame);
            }
        }


        private void Initialize()
        {
            //Disabilito l'echo dei comandi perchè inficia sulle prestazioni.
            this.SetEchoMode(false);

            //Disabilito eventuali connessioni precedenti.
            this.CloseDataConnection();

            //Disabilito la modalità di connessione trasparente perchè altrimenti il parser
            // non gestisce correttamente le frame di invio e ricezione dati.
            this.SetConnectionMode(false);

            //Disabilito la gestione delle connessioni multiple perchè al momento non è gestito.
            // In futuro si potrebbe gestire la modalità multiconnessione.
            this.StartUpMultiIPConnection(false);

            //Disabilito la visualizzazione del prompt quando la richiesta di invio riesce
            // perchè al momento non è gestito.
            this.SetPromptWhenModuleSendsData(1);

            //Aggiungo l'intestazione per i pacchetti IP ricevuti perchè altrimenti il parser
            // non riconosce correttamente le frame di ricezione dei dati.
            this.AddIPHead(true);
        }

        public override void Close()
        {
            this.protocol.Close();
        }

        public int GetCallWaitingControl()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CCWA, ATCommandType.Read, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            return Convert.ToInt32(responseFrame.OutParameters);
        }

        public void SetEchoMode(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(mode ? ATCommand.ATE1 : ATCommand.ATE0, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void AddIPHead(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPHEAD, ATCommandType.Write, mode ? "1" : "0");

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        private void SetAPNSettings(string apn, string username, string password)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CSTT, ATCommandType.Write,
                (username != null && username.Length > 0 && password != null && password.Length > 0) ?
                String.Concat("\"", apn, "\",\"", username, "\",\"", password, "\"") : String.Concat("\"", apn, "\""));

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void SetDNSSettings(string primaryIPAddress, string secondaryIPAddress)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CDNSCFG, ATCommandType.Write, String.Concat(primaryIPAddress, ",", secondaryIPAddress));

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void BringUpWirelessConnection()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIICR, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void OpenDataConnection()
        {
            string signalQualityReport;
            string networkRegistration;
            string gprsServiceState;
            string localIPAddress;

            signalQualityReport = this.GetSignalQualityReport();
            networkRegistration = this.GetGsmNetworkRegistration();
            gprsServiceState = this.GetGPRSServiceState();

            this.SetAPNSettings(this.AccessPointName, this.AccessUsername, this.AccessPassword);

            this.BringUpWirelessConnection();

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

        public void SetPromptWhenModuleSendsData(int mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSPRT, ATCommandType.Write, mode.ToString());

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void ConnectIPClient(string mode, string ipAddress, int port)
        {
            this.ConnectIPClient(mode, ipAddress, port, Timeout.Infinite);
        }

        public override void ConnectIPClient(string mode, string ipAddress, int port, int timeout)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSTART, ATCommandType.Write,
                String.Concat("\"", mode, "\",\"", ipAddress, "\",", port.ToString()));

            this.connectionEvent.Reset();

            lock (connectionSyncRoot)
            {
                responseFrame = (ATFrame)this.protocol.Process(requestFrame);

                if (responseFrame.Result != "OK")
                    throw new ATModemException(ATModemError.Generic);

                if (!this.connectionEvent.WaitOne(timeout, true))
                    throw new ATModemException(ATModemError.Timeout);

                this.OnClientConnected(EventArgs.Empty);
            }
        }

        public override string QueryDNSIPAddress(string domainName)
        {
            return this.QueryDNSIPAddress(domainName, Timeout.Infinite);
        }

        public override string QueryDNSIPAddress(string domainName, int timeout)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CDNSGIP, ATCommandType.Write,
                String.Concat("\"" + domainName + "\""));
            string[] dnsParametersTokens = null;
            int endTimeoutTicks = 0;

            if (timeout != Timeout.Infinite)
                endTimeoutTicks = Environment.TickCount + timeout;

            do
            {
                this.dnsQueryEvent.Reset();

                responseFrame = (ATFrame)this.protocol.Process(requestFrame);

                if (responseFrame.Result != "OK")
                    throw new ATModemException(ATModemError.Generic);

                if (!this.dnsQueryEvent.WaitOne(timeout, true))
                    throw new ATModemException(ATModemError.Timeout);

                dnsParametersTokens = this.dnsQueryParameters.Split(',');

                if (timeout != Timeout.Infinite)
                    timeout -= endTimeoutTicks - Environment.TickCount;
            }
            while ((dnsParametersTokens == null || dnsParametersTokens.Length < 1 || dnsParametersTokens[0] != "1") &&
                (timeout == Timeout.Infinite || timeout > 0));

            if (dnsParametersTokens == null || dnsParametersTokens.Length < 1 || dnsParametersTokens[0] != "1")
                throw new ATModemException(ATModemError.Generic);

            return dnsParametersTokens[2].Trim('"');
        }

        public override void SendData(byte[] buffer, int index, int count)
        {
            this.SendData(buffer, index, count, Timeout.Infinite);
        }

        public override void SendData(byte[] buffer, int index, int count, int timeout)
        {
            lock (this.protocol)
            {
                ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSEND, ATCommandType.Execution, null);

                this.sendEvent.Reset();
                this.sendPromptEvent.Reset();

                this.protocol.Send(requestFrame);

                if (!this.sendPromptEvent.WaitOne(timeout, true))
                    throw new ATModemException(ATModemError.Timeout);

                requestFrame.DataStream.Write(buffer, index, count);

                requestFrame.DataStream.Write(new byte[] { 0x1A }, 0, 1);

                if (!this.sendEvent.WaitOne(timeout, true))
                    throw new ATModemException(ATModemError.Timeout);

                if (this.sendResult != "OK")
                    throw new ATModemException(ATModemError.Generic);
            }
        }

        public override void DisconnectIPClient()
        {
            lock (this.connectionSyncRoot)
            {
                if (this.clientConnected)
                {
                    ATFrame responseFrame;
                    ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPCLOSE, ATCommandType.Execution, null);

                    try
                    {
                        responseFrame = (ATFrame)this.protocol.Process(requestFrame);

                        if (responseFrame.Result == "ERROR")
                            throw new ATModemException(ATModemError.Generic);
                    }
                    catch (ATModemException)
                    {
                        if (this.GetConnectionStatus().IndexOf("CLOSED") == -1)
                            throw new ATModemException(ATModemError.Generic);
                    }

                    this.OnClientDisconnected(EventArgs.Empty);
                }
            }
        }

        public override void CloseDataConnection()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSHUT, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result == "ERROR")
                throw new ATModemException(ATModemError.Generic);
        }

        public override string GetPinRequired()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CPIN, ATCommandType.Read, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetSignalQualityReport()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CSQ, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetGsmNetworkRegistration()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CREG, ATCommandType.Read, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetGprsNetworkRegistration()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CGREG, ATCommandType.Read, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetGPRSServiceState()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CGATT, ATCommandType.Read, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetImei()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_GSN, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetConnectionStatus()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = this.protocol.CreateRequestFrame(ATCommand.AT_CIPSTATUS, ATCommandType.Execution, null);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
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
    }
}
