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
using System.Diagnostics;

namespace BrusDev.IO.Modems
{
    public class ATProtocol
    {
        private const int defaultDataTimeout = 3000;
        private const int defaultProcessTimeout = 60000;
        private const int receivingBufferSize = 128;
        private const int sendingBufferSize = 128;
        private const byte byte_CarriageReturn = (byte)'\r';
        private const byte byte_LineFeed = (byte)'\n';

        private bool closed;

        private ATParser parser;
        private ATParserResult parserResult;

        private SerialPort serialPort;

        private int dataIndex;
        private int dataCount;
        private int dataLength;
        private int dataTicks;
        private int frameIndex;
        private int readDataLength;
        private object dataSyncRoot;
        private ATFrameData frameData;
        private ManualResetEvent dataReadEvent;
        private ManualResetEvent dataReadyEvent;

        private byte[] receivingBuffer;
        private int receivingBufferIndex;
        private int receivingBufferCount;
        private Thread receivingThread;

        private int sendingLength;
        private byte[] sendingBuffer;
        private DateTime lastSendDateTime;
        private object processFrameLock;

        private bool waitingResponse;
        private object waitingResponseLock;
        private ATFrame waitingFrame;
        private string waitingResponseCommand;
        private ATCommandType waitingResponseCommandType;
        private AutoResetEvent waitingResponseEvent;


        public event ATModemFrameEventHandler FrameReceived;
        public event ATModemFrameDataEventHandler FrameDataReceived;


        public ATProtocol(ATParser parser)
        {
            this.parser = parser;
            this.parserResult = null;

            this.closed = true;

            this.lastSendDateTime = DateTime.Now;

            this.sendingLength = 0;
            this.sendingBuffer = new byte[sendingBufferSize];
            this.processFrameLock = new object();

            this.waitingResponseLock = new object();
            this.waitingResponseEvent = new AutoResetEvent(false);

            this.readDataLength = 0;
            this.dataSyncRoot = new object();
            this.dataReadEvent = new ManualResetEvent(false);
            this.dataReadyEvent = new ManualResetEvent(false);
        }

        public void Open(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake)
        {
            this.closed = false;

            this.serialPort = new SerialPort(portName);
            this.serialPort.BaudRate = baudRate;
            this.serialPort.Parity = parity;
            this.serialPort.DataBits = dataBits;
            this.serialPort.StopBits = stopBits;
            this.serialPort.Handshake = handshake;

            this.serialPort.ReadTimeout = Timeout.Infinite;
            this.serialPort.WriteTimeout = 3000;

            this.serialPort.Open();

            this.receivingBuffer = new byte[receivingBufferSize];
            this.receivingThread = new Thread(this.ReceiverThreadRun);
            this.receivingThread.Start();
        }

        public void Close()
        {
            this.closed = true;

            this.serialPort.Close();
        }

        public void WriteData(byte[] buffer, int index, int count)
        {
            this.serialPort.Write(buffer, index, count);

#if (DEBUG)
            Debug.Print("WriteData data > ");
            Debug.Print(new string(ATParser.Bytes2Chars(buffer, index, count)));
#endif
        }

        public void Send(ATFrame frame)
        {
            lock (this.sendingBuffer)
            {
                this.sendingLength = frame.GetBytes(this.sendingBuffer, 0);

                this.serialPort.Write(this.sendingBuffer, 0, this.sendingLength);

#if (DEBUG)
                Debug.Print("Sent frame > ");
                Debug.Print(new string(ATParser.Bytes2Chars(this.sendingBuffer, 0, this.sendingLength)));
#endif

                this.lastSendDateTime = DateTime.Now;
            }

        }

        public ATFrame Process(ATFrame frame)
        {
            return this.Process(frame, defaultProcessTimeout);
        }

        public ATFrame Process(ATFrame frame, int timeout)
        {
            lock (this.processFrameLock)
            {
                lock (this.waitingResponseLock)
                {
                    this.waitingResponse = true;
                    this.waitingResponseCommand = frame.Command;
                    this.waitingResponseCommandType = frame.CommandType;
                }

                try
                {
                    this.Send(frame);

                    if (this.waitingResponseEvent.WaitOne(timeout, true))
                    {
                        return this.waitingFrame;
                    }

                    throw new ATModemException(ATModemError.Timeout);
                }
                finally
                {
                    lock (this.waitingResponseLock)
                    {
                        this.waitingResponse = false;
                    }
                }
            }
        }

        private void ReceiverThreadRun()
        {
            try
            {
                int readBytes;
                int movingBytes;
                int firstDelimiterIndex;
                int secondDelimiterIndex;


                while (!this.closed)
                {
                    readBytes = this.serialPort.Read(this.receivingBuffer, this.receivingBufferIndex + this.receivingBufferCount,
                        receivingBufferSize - this.receivingBufferIndex - this.receivingBufferCount);

                    if (readBytes > 0)
                    {
                        this.receivingBufferCount += readBytes;

#if (DEBUG)
                        Debug.Print("Buffer > ");
                        Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount)));
#endif
                        while (this.receivingBufferCount > 0)
                        {
                            //Cerco il primo delimitatore.
                            firstDelimiterIndex = this.parser.IndexOfDelimitor(this.receivingBuffer,
                                this.receivingBufferIndex, this.receivingBufferCount, false);

                            //Verifico se ho trovato il delimitatore.
                            if (firstDelimiterIndex == -1)
                            {
                                //Cancello il contenuto del buffer.
                                this.receivingBufferIndex = 0;
                                this.receivingBufferCount = 0;

                                break;
                            }
                            else if (firstDelimiterIndex > this.receivingBufferIndex)
                            {
                                //Cancello il contenuto del buffer fino al primo delimitatore.
                                this.receivingBufferCount -= (firstDelimiterIndex - this.receivingBufferIndex);
                                this.receivingBufferIndex = firstDelimiterIndex;
                            }


                            if (this.receivingBufferCount > 0)
                            {
                                lock (this.waitingResponseLock)
                                {
                                    //Verifico se sono in attesa di una risposta.
                                    if (this.waitingResponse)
                                    {
                                        //Eseguo il parse della risposta attesa.
                                        this.parserResult = this.parser.ParseResponse(this.waitingResponseCommand,
                                            this.waitingResponseCommandType, this.receivingBuffer,
                                            this.receivingBufferIndex, this.receivingBufferCount);

                                        if (this.parserResult != null && parserResult.Success)
                                        {
                                            //Notifico la ricezione della risposta attesa.
                                            this.waitingResponse = false;
                                            this.waitingFrame = parserResult.Frame;
                                            this.waitingResponseEvent.Set();
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        //Eseguo il parse della risposta non attesa.
                                        this.parserResult = this.parser.ParseUnsolicitedResponse(this.receivingBuffer,
                                            this.receivingBufferIndex, this.receivingBufferCount);

                                        if (this.parserResult != null && parserResult.Success)
                                        {
                                            //Notifico la ricezione della risposta non attesa.
                                            if (this.FrameReceived != null)
                                                this.FrameReceived(this, ATModemFrameEventArgs.GetInstance(this.parserResult.Frame));
                                        }
                                        else
                                        {
                                            //Cerco il secondo delimitatore.
                                            secondDelimiterIndex = this.parser.IndexOfDelimitor(this.receivingBuffer,
                                                this.receivingBufferIndex, this.receivingBufferCount, true);

                                            if (secondDelimiterIndex > this.receivingBufferIndex)
                                            {
                                                //Cancello il contenuto del buffer fino al secondo delimitatore.
                                                this.receivingBufferCount -= (secondDelimiterIndex - this.receivingBufferIndex);
                                                this.receivingBufferIndex = secondDelimiterIndex;
                                            }

                                            break;
                                        }
                                    }
                                }


                                this.dataLength = parserResult.Frame.DataLength;

                                if (this.dataLength > 0)
                                {
                                    this.frameIndex = this.parserResult.Index;
                                    this.dataIndex = this.frameIndex + this.parserResult.Length;
                                    this.dataCount = 0;

                                    this.readDataLength = this.receivingBufferIndex + this.receivingBufferCount - this.dataIndex;

                                    if (this.readDataLength > this.dataLength)
                                        this.readDataLength = this.dataLength;

                                    this.frameData = ATFrameData.GetInstance(parserResult.Frame);

                                    if (this.readDataLength > 0)
                                    {
                                        this.frameData.Write(this.receivingBuffer, this.dataIndex, this.readDataLength);

#if (DEBUG)
                                        Debug.Print("ReadData data > ");
                                        Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.dataIndex, this.readDataLength)));
#endif

                                        this.dataCount += this.readDataLength;

                                        //Notifico la ricezione dei dati della risposta.
                                        if (this.FrameDataReceived != null)
                                            this.FrameDataReceived(this, ATModemFrameDataEventArgs.GetInstance(this.frameData));

                                        this.receivingBufferCount -= this.readDataLength;
                                        this.readDataLength = 0;
                                    }

                                    //Check if read bytes from the serial port.
                                    if (this.dataCount < this.dataLength)
                                    {
                                        while (this.dataCount < this.dataLength)
                                        {
                                            this.dataTicks = Environment.TickCount;

                                            readBytes = this.serialPort.Read(this.receivingBuffer, this.frameIndex, receivingBufferSize - this.frameIndex);

#if (DEBUG)
                                            Debug.Print("Buffer > ");
                                            Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.frameIndex, readBytes)));
#endif

                                            //Check the data timeout.
                                            if (Environment.TickCount - this.dataTicks > defaultDataTimeout)
                                            {
                                                this.readDataLength = 0;
                                                break;
                                            }

                                            this.readDataLength = this.dataLength - this.dataCount;

                                            if (readBytes < this.readDataLength)
                                                this.readDataLength = readBytes;

                                            this.frameData.Write(this.receivingBuffer, this.frameIndex, this.readDataLength);

#if (DEBUG)
                                            Debug.Print("ReadData data > ");
                                            Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.frameIndex, this.readDataLength)));
#endif

                                            this.dataCount += this.readDataLength;

                                            //Notifico la ricezione dei dati della risposta.
                                            if (this.FrameDataReceived != null)
                                                this.FrameDataReceived(this, ATModemFrameDataEventArgs.GetInstance(this.frameData));
                                        }

                                        this.receivingBufferCount += (readBytes - this.parserResult.Length);
                                        this.readDataLength -= this.parserResult.Length;
                                    }
                                }
                                else
                                {
                                    this.readDataLength = 0;
                                }


                                //Cancello il contenuto del buffer relativo alla risposta e ai dati.
                                movingBytes = this.receivingBufferIndex + this.receivingBufferCount -
                                    this.parserResult.Index - this.parserResult.Length - this.readDataLength;

                                if (movingBytes > 0)
                                {
                                    Array.Copy(this.receivingBuffer, this.receivingBufferIndex + this.receivingBufferCount -
                                        movingBytes, this.receivingBuffer, parserResult.Index, movingBytes);
                                }

                                this.receivingBufferCount -= (parserResult.Length + this.readDataLength);
                            }
                        }

                        //Verifico lo stato del buffer.
                        if (this.receivingBufferCount == 0)
                        {
                            this.receivingBufferIndex = 0;
                        }
                        else if (this.receivingBufferIndex + this.receivingBufferCount == receivingBufferSize)
                        {
                            if (this.receivingBufferIndex == 0)
                                throw new OutOfMemoryException();

                            Array.Copy(this.receivingBuffer, this.receivingBufferIndex,
                                this.receivingBuffer, 0, this.receivingBufferCount);

                            this.receivingBufferIndex = 0;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                exception.ToString();
            }
        }
    }
}
