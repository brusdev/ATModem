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
        private const int defaultProcessTimeout = 60000;
        private const int receivingBufferSize = 128;
        private const int sendingBufferSize = 128;
        private const byte byte_CarriageReturn = (byte)'\r';
        private const byte byte_LineFeed = (byte)'\n';

        private bool closed;

        private ATParser parser;
        private ATParserResult parserResult;

        private SerialPort serialPort;

        private Queue framesQueue;
        private AutoResetEvent framesQueueIsReady;

        private int dataIndex;
        private int dataCount;
        private int dataLength;
        private int readDataLength;
        private object dataSyncRoot;
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


        public event ATModemEventHandler FrameReceived;


        public int FramesToReceive { get { lock (this.framesQueue) { return this.framesQueue.Count; } } }


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

            this.framesQueue = new Queue();
            this.framesQueueIsReady = new AutoResetEvent(false);

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

            Debug.Print("WriteData data > ");
            Debug.Print(new string(ATParser.Bytes2Chars(buffer, index, count)));
        }

        public ATFrame Receive()
        {
            return this.Receive(Timeout.Infinite);
        }

        public ATFrame Receive(int timeout)
        {
            ATFrame frame = null;


            if (!this.framesQueueIsReady.WaitOne(timeout, true))
                throw new ATModemException(ATModemError.Timeout);


            lock (this.framesQueue)
            {
                frame = (ATFrame)this.framesQueue.Dequeue();

                if (this.framesQueue.Count == 0)
                    this.framesQueueIsReady.Reset();
                else
                    this.framesQueueIsReady.Set();
            }

            return frame;
        }

        public void Send(ATFrame frame)
        {
            lock (this.sendingBuffer)
            {
                this.sendingLength = frame.GetBytes(this.sendingBuffer, 0);

                this.serialPort.Write(this.sendingBuffer, 0, this.sendingLength);

                Debug.Print("Sent frame > ");
                Debug.Print(new string(ATParser.Bytes2Chars(this.sendingBuffer, 0, this.sendingLength)));

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

                        Debug.Print("Buffer > ");
                        Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount)));

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
                                //Cerco il secondo delimitatore.
                                secondDelimiterIndex = this.parser.IndexOfDelimitor(this.receivingBuffer,
                                    this.receivingBufferIndex, this.receivingBufferCount, true);

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
                                            this.ReadData();

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
                                            this.ReadData();

                                            //Notifico la ricezione della risposta non attesa.
                                            lock (this.framesQueue)
                                            {
                                                this.framesQueue.Enqueue(parserResult.Frame);

                                                this.framesQueueIsReady.Set();

                                                if (this.FrameReceived != null)
                                                    this.FrameReceived(this, EventArgs.Empty);
                                            }
                                        }
                                        else
                                        {
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

                                //Cancello il contenuto del buffer relativo alla risposta e ai dati.
                                movingBytes = this.receivingBufferIndex + this.receivingBufferCount -
                                    this.parserResult.Index - this.parserResult.Length - this.readDataLength;
                                Array.Copy(this.receivingBuffer, parserResult.Index + parserResult.Length,
                                    this.receivingBuffer, parserResult.Index, movingBytes);
                                this.receivingBufferCount = this.receivingBufferCount - parserResult.Length - this.readDataLength;
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

        private void ReadData()
        {
            int readingDataCount;


            this.dataLength = parserResult.Frame.DataLength;


            if (this.dataLength > 0)
            {
                this.dataIndex = this.parserResult.Index + this.parserResult.Length;
                this.dataCount = 0;

                this.readDataLength = this.receivingBufferIndex + this.receivingBufferCount - this.dataIndex;

                this.parserResult.Frame.Data = new byte[this.dataLength];


                if (this.readDataLength > 0)
                {
                    Array.Copy(this.receivingBuffer, this.dataIndex, parserResult.Frame.Data, 0, this.readDataLength);

                    Debug.Print("ReadData data > ");
                    Debug.Print(new string(ATParser.Bytes2Chars(parserResult.Frame.Data, 0, this.readDataLength)));

                    this.dataCount += this.readDataLength;
                }

                //Check if read bytes from the serial port.
                while (this.dataCount < this.dataLength)
                {
                    readingDataCount = this.serialPort.Read(parserResult.Frame.Data, this.dataCount, this.dataLength - this.dataCount);

                    Debug.Print("ReadData data > ");
                    Debug.Print(new string(ATParser.Bytes2Chars(parserResult.Frame.Data, this.dataCount, readingDataCount)));

                    this.dataCount += readingDataCount;
                }
            }
            else
            {
                this.readDataLength = 0;
            }
        }
    }
}
