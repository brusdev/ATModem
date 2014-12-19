using BrusDev.IO.Modems.Frames;
using BrusDev.IO.Modems.Parsers;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif
using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace BrusDev.IO.Modems
{
    public class ATProtocol: IDisposable
    {
        private const int defaultDataTimeout = 500;
        private const int defaultProcessTimeout = 5000;
        private const int receivingBufferSize = 128;
        private const int sendingBufferSize = 128;
        private const byte byte_CarriageReturn = (byte)'\r';
        private const byte byte_LineFeed = (byte)'\n';

        private bool closed;
        private bool disposed = false;

        private ATParser parser;
        private ATParserResult parserResult;

        private Stream stream;

        private int dataIndex;
        private int dataCount;
        private int dataLength;
        private int dataTicks;
        private int dataTime;
        private int frameIndex;
        private int frameLength;
        private int readDataLength;
        private object dataSyncRoot;
        private ATResponseDataStream dataStream;
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

        class ATRequestDataStream : System.IO.Stream
        {
            private static ATRequestDataStream instance = new ATRequestDataStream(null);

            public static ATRequestDataStream GetInstance(ATProtocol protocol)
            {
                instance.protocol = protocol;

                return instance;
            }

            ATProtocol protocol;

            public ATRequestDataStream(ATProtocol protocol)
            {
                this.protocol = protocol;
            }

            public override bool CanRead
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanSeek
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanWrite
            {
                get { throw new NotImplementedException(); }
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override long Length
            {
                get { throw new NotImplementedException(); }
            }

            public override long Position
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, System.IO.SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.protocol.Write(buffer, offset, count);
            }
        }

        class ATResponseDataStream : System.IO.Stream
        {
            private const int bufferSize = 256;

            private static ATResponseDataStream instance = new ATResponseDataStream(0);

            public static ATResponseDataStream GetInstance(int length)
            {
                instance.Reset();

                instance.length = length;

                return instance;
            }

            private bool closed;
            private bool discarded;

            private int count;
            private int free;
            private int head;
            private int length;
            private int rightHead;
            private int rightTail;
            private int tail;
            private int used;
            private byte[] buffer;
            private AutoResetEvent bufferReady;

            public ATResponseDataStream(int length)
            {
                this.buffer = new byte[bufferSize];
                this.bufferReady = new AutoResetEvent(false);

                this.Reset();
            }

            public void Reset()
            {
                lock (this.buffer)
                {
                    this.closed = false;
                    this.discarded = false;

                    this.count = 0;
                    this.free = bufferSize;
                    this.head = 0;
                    this.length = 0;
                    this.rightTail = 0;
                    this.rightHead = 0;
                    this.tail = 0;
                    this.used = 0;

                    this.bufferReady.Reset();
                }
            }

            public override void Close()
            {
                base.Close();

                lock (this.buffer)
                {
                    this.closed = true;

                    this.bufferReady.Set();
                }
            }

            public void WriteBuffer(byte[] buffer, int offset, int count)
            {
                lock (this.buffer)
                {
                    int discarded = count - free;

                    if (discarded > 0)
                    {
                        count = this.free;

                        this.discarded = true;
                    }

                    this.rightTail = bufferSize - this.tail;

                    if (count < this.rightTail)
                    {
                        Array.Copy(buffer, offset, this.buffer, this.tail, count);

                        this.tail += count;

                        this.free -= count;
                        this.used += count;
                    }
                    else
                    {
                        Array.Copy(buffer, offset, this.buffer, this.tail, this.rightTail);

                        this.tail = count - this.rightTail;

                        Array.Copy(buffer, offset + this.rightTail, this.buffer, 0, this.tail);

                        this.free -= count;
                        this.used += count;
                    }

                    if (this.used > 0)
                    {
                        this.bufferReady.Set();
                    }
                }
            }

            public override bool CanRead
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanSeek
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanWrite
            {
                get { throw new NotImplementedException(); }
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override long Length
            {
                get { return this.length; }
            }

            public override long Position
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                this.bufferReady.WaitOne();

                lock (this.buffer)
                {
                    if (this.closed || this.discarded)
                    {
                        this.bufferReady.Set();

                        return 0;
                    }

                    if (count > this.used)
                        count = this.used;

                    this.rightHead = bufferSize - this.head;

                    if (count < this.rightHead)
                    {
                        Array.Copy(this.buffer, this.head, buffer, offset, count);

                        this.head += count;

                        this.free += count;
                        this.used -= count;
                    }
                    else
                    {
                        Array.Copy(this.buffer, this.head, buffer, offset, this.rightHead);

                        this.head = count - this.rightHead;

                        Array.Copy(this.buffer, 0, buffer, offset + this.rightHead, this.head);

                        this.free += count;
                        this.used -= count;
                    }

                    this.count += count;

                    if (this.count == this.length)
                    {
                        this.Close();
                    }


                    if (this.used > 0)
                    {
                        this.bufferReady.Set();
                    }
                    else
                    {
                        this.bufferReady.Reset();
                    }

                    return count;
                }
            }

            public override long Seek(long offset, System.IO.SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }


        public ATProtocol(Stream stream, ATParser parser)
        {
            this.stream = stream;

            this.parser = parser;
            this.parserResult = null;

            this.closed = false;

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

            this.receivingBuffer = new byte[receivingBufferSize];
            this.receivingThread = new Thread(this.ReceiverThreadRun);
            this.receivingThread.Start();
        }

        public void Close()
        {
            this.closed = true;

            this.stream.Close();
        }

        public ATFrame CreateRequestFrame(string command, ATCommandType commandType, string inParameters)
        {
            return ATFrame.GetInstance(command, commandType, ATRequestDataStream.GetInstance(this), inParameters);
        }

        private void Write(byte[] buffer, int index, int count)
        {
            this.stream.Write(buffer, index, count);

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

                this.stream.Write(this.sendingBuffer, 0, this.sendingLength);

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
                    readBytes = this.stream.Read(this.receivingBuffer, this.receivingBufferIndex + this.receivingBufferCount,
                        receivingBufferSize - this.receivingBufferIndex - this.receivingBufferCount);

                    if (readBytes > 0)
                    {
                        this.receivingBufferCount += readBytes;

#if (DEBUG)
                        Debug.Print("ReadBuffer > " + readBytes);
                        //Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount)));
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
                                            this.dataLength = this.parserResult.DataLength;
                                            this.dataStream = this.dataLength > 0 ? ATResponseDataStream.GetInstance(this.dataLength) : null;

                                            //Notifico la ricezione della risposta attesa.
                                            this.waitingResponse = false;
                                            this.waitingFrame = ATFrame.GetInstance(parserResult.Command, parserResult.CommandType, this.dataStream, parserResult.Unsolicited, parserResult.Result, parserResult.OutParameters);
                                            this.waitingResponseEvent.Set();
                                        }
                                        else
                                        {
#if (DEBUG)
                                            Debug.Print("ResponseBuffer > " + this.receivingBufferCount);
                                            Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount)));
#endif

                                            if (this.receivingBufferCount == receivingBufferSize)
                                            {
                                                //Cancello il contenuto del buffer.
                                                this.receivingBufferIndex = 0;
                                                this.receivingBufferCount = 0;
                                            }

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
                                            this.dataLength = this.parserResult.DataLength;
                                            this.dataStream = this.dataLength > 0 ? ATResponseDataStream.GetInstance(this.dataLength) : null;

                                            //Notifico la ricezione della risposta non attesa.
                                            if (this.FrameReceived != null)
                                                this.FrameReceived(this, ATModemFrameEventArgs.GetInstance(ATFrame.GetInstance(parserResult.Command, parserResult.CommandType, this.dataStream, parserResult.Unsolicited, parserResult.Result, parserResult.OutParameters)));
                                        }
                                        else
                                        {
#if (DEBUG)
                                            Debug.Print("UnsolicitedResponseBuffer > " + this.receivingBufferCount);
                                            Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount)));
#endif

                                            //Cerco il secondo delimitatore.
                                            secondDelimiterIndex = this.parser.IndexOfDelimitor(this.receivingBuffer,
                                                this.receivingBufferIndex + 1, this.receivingBufferCount - 1, true);

                                            if (secondDelimiterIndex != -1)
                                            {
                                                //Cancello il contenuto del buffer fino al secondo delimitatore.
                                                this.receivingBufferCount -= (secondDelimiterIndex - this.receivingBufferIndex);
                                                this.receivingBufferIndex = secondDelimiterIndex;
                                            }
                                            else if (this.receivingBufferCount == receivingBufferSize)
                                            {
                                                //Cancello il contenuto del buffer.
                                                this.receivingBufferIndex = 0;
                                                this.receivingBufferCount = 0;
                                            }

                                            break;
                                        }
                                    }
                                }


                                this.frameIndex = this.parserResult.Index;
                                this.frameLength = this.parserResult.Length;

                                if (this.dataLength > 0)
                                {
                                    this.dataIndex = this.frameIndex + this.frameLength;
                                    this.dataCount = 0;

                                    this.readDataLength = this.receivingBufferIndex + this.receivingBufferCount - this.dataIndex;

                                    if (this.readDataLength > this.dataLength)
                                        this.readDataLength = this.dataLength;

                                    if (this.readDataLength > 0)
                                    {
                                        this.dataCount += this.readDataLength;
#if (DEBUG)
                                        Debug.Print("ReadData > " + this.readDataLength + "/" + this.dataCount);
                                        //Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.dataIndex, this.readDataLength)));
#endif

                                        this.dataStream.WriteBuffer(this.receivingBuffer, this.dataIndex, this.readDataLength);

                                        this.receivingBufferCount -= this.readDataLength;
                                        this.readDataLength = 0;
                                    }

                                    //Check if read bytes from the serial port.
                                    if (this.dataCount < this.dataLength)
                                    {
                                        while (this.dataCount < this.dataLength)
                                        {
                                            this.dataTicks = Environment.TickCount;

                                            readBytes = this.stream.Read(this.receivingBuffer, this.frameIndex, receivingBufferSize - this.frameIndex);

                                            this.dataTime = Environment.TickCount - this.dataTicks;

#if (DEBUG)
                                            Debug.Print("ReadDataBuffer > " + readBytes + "/" + this.dataTime);
                                            //Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.frameIndex, readBytes)));
#endif

                                            lock (this.waitingResponseLock)
                                            {
                                                //Verifico se sono in attesa di una risposta o se è scaduto il data timeout.
                                                if (this.waitingResponse || this.dataTime > defaultDataTimeout)
                                                {
                                                    this.dataStream.Close();

                                                    this.readDataLength = 0;

                                                    break;
                                                }
                                            }

                                            this.readDataLength = this.dataLength - this.dataCount;

                                            if (readBytes < this.readDataLength)
                                                this.readDataLength = readBytes;

                                            this.dataCount += this.readDataLength;

#if (DEBUG)
                                            Debug.Print("ReadData > " + this.readDataLength + "/" + this.dataCount);
                                            //Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.frameIndex, this.readDataLength)));
#endif

                                            this.dataStream.WriteBuffer(this.receivingBuffer, this.frameIndex, this.readDataLength);
                                        }

                                        this.receivingBufferCount += (readBytes - this.frameLength);
                                        this.readDataLength -= this.frameLength;
                                    }
                                }
                                else
                                {
                                    this.readDataLength = 0;
                                }


                                //Cancello il contenuto del buffer relativo alla risposta e ai dati.
                                movingBytes = this.receivingBufferIndex + this.receivingBufferCount -
                                    this.frameIndex - this.frameLength - this.readDataLength;

                                if (movingBytes > 0)
                                {
                                    Array.Copy(this.receivingBuffer, this.receivingBufferIndex + this.receivingBufferCount -
                                        movingBytes, this.receivingBuffer, this.frameIndex, movingBytes);
                                }

                                this.receivingBufferCount -= (this.frameLength + this.readDataLength);
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
                Debug.Print("ExceptionResponseBuffer > " + this.receivingBufferCount);
                Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount)));

                exception.ToString();
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.Close();
                }

                disposed = true;
            }
        }

        ~ATProtocol()
        {
            Dispose(false);
        }
    }
}
