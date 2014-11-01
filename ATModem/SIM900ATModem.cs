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
    public class SIM900ATModem : ATModem
    {
        private ATProtocol protocol;

        private ManualResetEvent connectionEvent;

        private string dnsQueryParameters;
        private ManualResetEvent dnsQueryEvent;

        private string sendResult;
        private ManualResetEvent sendEvent;

        private ManualResetEvent sendPromptEvent;

        private object connectionSyncRoot;


        public SIM900ATModem()
        {
            this.protocol = new ATProtocol(new SIM900ATParser());
            this.protocol.FrameReceived += protocol_FrameReceived;

            this.connectionEvent = new ManualResetEvent(false);
            this.dnsQueryEvent = new ManualResetEvent(false);
            this.sendEvent = new ManualResetEvent(false);
            this.sendPromptEvent = new ManualResetEvent(false);

            this.connectionSyncRoot = new object();
        }

        public void protocol_FrameReceived(object sender, EventArgs e)
        {
            ATFrame responseFrame = null;
            int framesToReceive = this.protocol.FramesToReceive;

            for (int frameIndex = 0; frameIndex < framesToReceive; frameIndex++)
            {
                responseFrame = this.protocol.Receive();

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
                else if (responseFrame.Command == ATCommand.IPD)
                {
                    this.AddReceivedData(responseFrame.Data);
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
            }
        }

        public override void Open(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake)
        {
            this.protocol.Open(portName, baudRate, parity, dataBits, stopBits, handshake);

            this.Initialize();
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
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CCWA;
            requestFrame.CommandType = ATCommandType.Read;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            return Convert.ToInt32(responseFrame.OutParameters);
        }

        public void SetEchoMode(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = mode ? ATCommand.ATE1 : ATCommand.ATE0;
            requestFrame.CommandType = ATCommandType.Execution;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void AddIPHead(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIPHEAD;
            requestFrame.CommandType = ATCommandType.Write;
            requestFrame.InParameters = (mode ? "1" : "0");

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void SetAPNSettings(string apn, string username, string password)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CSTT;
            requestFrame.CommandType = ATCommandType.Write;
            if (username != null && username.Length > 0 && password != null && password.Length > 0)
            {
                requestFrame.InParameters = String.Concat("\"", apn, "\",\"", username, "\",\"", password, "\"");
            }
            else if (username != null && username.Length > 0 && password != null && password.Length > 0)
            {
                requestFrame.InParameters = String.Concat("\"", apn, "\",\"", password, "\"");
            }
            else
            {
                requestFrame.InParameters = String.Concat("\"", apn, "\"");
            }


            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public override void SetDNSSettings(string primaryIPAddress, string secondaryIPAddress)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CDNSCFG;
            requestFrame.CommandType = ATCommandType.Write;
            requestFrame.InParameters = String.Concat(primaryIPAddress, ",", secondaryIPAddress);

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void BringUpWirelessConnection()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIICR;
            requestFrame.CommandType = ATCommandType.Execution;

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
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIFSR;
            requestFrame.CommandType = ATCommandType.Execution;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result == "ERROR")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters.Trim('"');
        }

        public void SetConnectionMode(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIPMODE;
            requestFrame.CommandType = ATCommandType.Write;
            requestFrame.InParameters = (mode ? "1" : "0");

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void StartUpMultiIPConnection(bool mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIPMUX;
            requestFrame.CommandType = ATCommandType.Write;
            requestFrame.InParameters = (mode ? "1" : "0");

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);
        }

        public void SetPromptWhenModuleSendsData(int mode)
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIPSPRT;
            requestFrame.CommandType = ATCommandType.Write;
            requestFrame.InParameters = mode.ToString();

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
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIPSTART;
            requestFrame.CommandType = ATCommandType.Write;
            requestFrame.InParameters = String.Concat("\"", mode, "\",\"", ipAddress, "\",", port.ToString());

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
            ATFrame requestFrame = ATFrame.Instance;
            string[] dnsParametersTokens = null;
            long endTimeoutTicks = 0;

            if(timeout != Timeout.Infinite)
#if MF_FRAMEWORK
                endTimeoutTicks = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks
                    + timeout * System.TimeSpan.TicksPerMillisecond;
#else
                endTimeoutTicks = DateTime.Now.Ticks
                    + timeout * System.TimeSpan.TicksPerMillisecond;
#endif

                requestFrame.Command = ATCommand.AT_CDNSGIP;
            requestFrame.CommandType = ATCommandType.Write;
            requestFrame.InParameters = String.Concat("\"" + domainName + "\"");


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
#if MF_FRAMEWORK
                    timeout -= (int)((endTimeoutTicks - Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks) / System.TimeSpan.TicksPerMillisecond);
#else
                    timeout -= (int)((endTimeoutTicks - DateTime.Now.Ticks) / System.TimeSpan.TicksPerMillisecond);
#endif
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
                ATFrame requestFrame = ATFrame.Instance;


                requestFrame.Command = ATCommand.AT_CIPSEND;
                requestFrame.CommandType = ATCommandType.Execution;

                this.sendEvent.Reset();
                this.sendPromptEvent.Reset();

                this.protocol.Send(requestFrame);

                if (!this.sendPromptEvent.WaitOne(timeout, true))
                    throw new ATModemException(ATModemError.Timeout);

                this.protocol.WriteData(buffer, index, count);

                this.protocol.WriteData(new byte[] { 0x1A }, 0, 1);

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
                    ATFrame requestFrame = ATFrame.Instance;

                    requestFrame.Command = ATCommand.AT_CIPCLOSE;
                    requestFrame.CommandType = ATCommandType.Execution;

                    responseFrame = (ATFrame)this.protocol.Process(requestFrame);

                    if (responseFrame.Result == "ERROR" && this.GetConnectionStatus() == "5" || this.GetConnectionStatus() == "6")
                        throw new ATModemException(ATModemError.Generic);

                    this.OnClientDisconnected(EventArgs.Empty);
                }
            }
        }

        public override void CloseDataConnection()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIPSHUT;
            requestFrame.CommandType = ATCommandType.Execution;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result == "ERROR")
                throw new ATModemException(ATModemError.Generic);
        }

        public override string GetPinRequired()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CPIN;
            requestFrame.CommandType = ATCommandType.Read;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetSignalQualityReport()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CSQ;
            requestFrame.CommandType = ATCommandType.Execution;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetGsmNetworkRegistration()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CREG;
            requestFrame.CommandType = ATCommandType.Read;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetGprsNetworkRegistration()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CGREG;
            requestFrame.CommandType = ATCommandType.Read;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetGPRSServiceState()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CGATT;
            requestFrame.CommandType = ATCommandType.Read;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetImei()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_GSN;
            requestFrame.CommandType = ATCommandType.Execution;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }

        public override string GetConnectionStatus()
        {
            ATFrame responseFrame;
            ATFrame requestFrame = ATFrame.Instance;

            requestFrame.Command = ATCommand.AT_CIPSTATUS;
            requestFrame.CommandType = ATCommandType.Execution;

            responseFrame = (ATFrame)this.protocol.Process(requestFrame);

            if (responseFrame.Result != "OK")
                throw new ATModemException(ATModemError.Generic);

            return responseFrame.OutParameters;
        }
    }
}
