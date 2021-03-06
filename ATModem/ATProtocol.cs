﻿using BrusDev.IO.Modems.Frames;
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
        private const int defaultEchoTimeout = 3000;
        private const int defaultProcessTimeout = 3000;
        private const int receivingBufferSize = 128;
        private const int sendingBufferSize = 128;
        private const byte byte_CarriageReturn = (byte)'\r';
        private const byte byte_LineFeed = (byte)'\n';

        private bool closed;
        private bool disposed = false;
        private bool echoEnabled;
        private object syncRoot;

        private ATParser parser;
        private ATParserResult parserResult;

        private Stream stream;

        private int readBytes;
        private int movingBytes;
        private int firstDelimiterIndex;
        private int secondDelimiterIndex;

        private int dataIndex;
        private int dataCount;
        private int dataLength;
        private int dataTicks;
        private int dataTime;
        private int frameIndex;
        private int frameLength;
        private int readDataLength;
        private ATResponseDataStream dataStream;

        private byte[] receivingBuffer;
        private int receivingBufferIndex;
        private int receivingBufferCount;
        private Thread receivingThread;

        private int sendingLength;
        private byte[] sendingBuffer;

        private bool waitingEcho;
        private bool waitingResponse;
        private int waitingTimeout;
        private object waitingLock;
        private ATFrame waitingFrame;
        private string waitingCommand;
        private ATCommandType waitingCommandType;
        private AutoResetEvent waitingEchoEvent;
        private AutoResetEvent waitingResponseEvent;
        private AsyncCallback waitingCallback;
        private WaitingAsyncResult waitingAsyncResult;


        public object SyncRoot { get { return this.syncRoot; } }

        public bool EchoEnabled { get { return this.echoEnabled; } set { this.echoEnabled = value; } }


        public event ATModemFrameEventHandler FrameReceived;


        #region DataStreams...

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
                lock (this.protocol.syncRoot)
                {
                    this.protocol.stream.Write(buffer, offset, count);
                }
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

        #endregion

        public class WaitingAsyncResult : IAsyncResult
        {
            private static WaitingAsyncResult instance = new WaitingAsyncResult();

            public static WaitingAsyncResult GetInstance()
            {
                return instance;
            }

            public object AsyncState { get; set; }
            public WaitHandle AsyncWaitHandle { get; set; }
            public bool CompletedSynchronously { get; set; }
            public bool IsCompleted { get; set; }

            public Exception AsyncException { get; set; }
            public ATFrame AsyncFrame { get; set; }

        }

        public ATProtocol(Stream stream, ATParser parser)
        {
            this.stream = stream;

            this.parser = parser;
            this.parserResult = null;

            this.closed = false;
            this.syncRoot = new object();
            this.echoEnabled = true;

            this.sendingLength = 0;
            this.sendingBuffer = new byte[sendingBufferSize];

            this.waitingLock = new object();
            this.waitingEchoEvent = new AutoResetEvent(false);
            this.waitingResponseEvent = new AutoResetEvent(false);

            this.readDataLength = 0;

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

        public IAsyncResult BeginProcess(ATFrame frame, bool waitResponse, int timeout, AsyncCallback callback, Object state)
        {
            lock (this.syncRoot)
            {
                lock (this.waitingLock)
                {
                    this.waitingEcho = this.echoEnabled;
                    this.waitingResponse = waitResponse;
                    this.waitingTimeout = Environment.TickCount + timeout;
                    this.waitingCommand = frame.Command;
                    this.waitingCommandType = frame.CommandType;

                    this.waitingCallback = callback;
                    this.waitingAsyncResult = WaitingAsyncResult.GetInstance();
                    this.waitingAsyncResult.AsyncState = state;
                }

                try
                {
                    this.sendingLength = frame.GetBytes(this.sendingBuffer, 0);

                    this.stream.Write(this.sendingBuffer, 0, this.sendingLength);

#if (DEBUG)
                    Debug.Print("Sent frame > ");
                    Debug.Print(new string(ATParser.Bytes2Chars(this.sendingBuffer, 0, this.sendingLength)));
#endif


                    return this.waitingAsyncResult;
                }
                catch
                {
                    lock (this.waitingLock)
                    {
                        this.waitingEcho = false;
                        this.waitingResponse = false;
                    }

                    throw new ATModemException(ATModemError.Generic);
                }
            }
        }

        public ATFrame EndProcess(IAsyncResult asyncResult)
        {
            WaitingAsyncResult waitingAsyncResult = (WaitingAsyncResult)asyncResult;

            if (waitingAsyncResult.AsyncException != null)
                throw waitingAsyncResult.AsyncException;

            return (ATFrame)waitingAsyncResult.AsyncFrame;
        }

        public ATFrame Process(ATFrame frame)
        {
            return this.Process(frame, true, defaultProcessTimeout);
        }

        public ATFrame Process(ATFrame frame, bool waitResponse)
        {
            return this.Process(frame, waitResponse, defaultProcessTimeout);
        }

        public ATFrame Process(ATFrame frame, bool waitResponse, int timeout)
        {
            return this.Process(frame, waitResponse, timeout, null);
        }
        public ATFrame Process(ATFrame frame, bool waitResponse, int timeout, AsyncCallback callback)
        {
            lock (this.syncRoot)
            {
                if (waitResponse)
                {
                    lock (this.waitingLock)
                    {
                        this.waitingEcho = this.echoEnabled;
                        this.waitingResponse = true;
                        this.waitingTimeout = Environment.TickCount + timeout;
                        this.waitingCommand = frame.Command;
                        this.waitingCommandType = frame.CommandType;

                        this.waitingCallback = null;

                        this.waitingEchoEvent.Reset();
                        this.waitingResponseEvent.Reset();
                    }
                }

                try
                {
                    this.sendingLength = frame.GetBytes(this.sendingBuffer, 0);

                    this.stream.Write(this.sendingBuffer, 0, this.sendingLength);

#if (DEBUG)
                    Debug.Print("Sent frame > ");
                    Debug.Print(new string(ATParser.Bytes2Chars(this.sendingBuffer, 0, this.sendingLength)));
#endif


                    if (this.waitingEcho && !this.waitingEchoEvent.WaitOne(defaultEchoTimeout, true))
                        throw new ATModemException(ATModemError.Timeout);


                    if (!waitResponse)
                        return null;

                    if (this.waitingResponseEvent.WaitOne(timeout, true))
                    {
                        return this.waitingFrame;
                    }

                    throw new ATModemException(ATModemError.Timeout);
                }
                finally
                {
                    if (waitResponse)
                    {
                        lock (this.waitingLock)
                        {
                            this.waitingEcho = false;
                            this.waitingResponse = false;
                        }
                    }
                }
            }
        }

        private void ReceiverThreadRun()
        {
            try
            {
                while (!this.closed)
                {
                    readBytes = this.stream.Read(this.receivingBuffer, this.receivingBufferIndex + this.receivingBufferCount,
                        receivingBufferSize - this.receivingBufferIndex - this.receivingBufferCount);

                    if (readBytes > 0)
                    {
                        this.receivingBufferCount += readBytes;

#if (DEBUG)
                        Debug.Print("ReadBuffer > " + readBytes);
                        Debug.Print(new string(ATParser.Bytes2Chars(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount)));
#endif
                        while (this.receivingBufferCount > 0)
                        {
                            lock (this.waitingLock)
                            {
                                this.parserResult = null;

                                //Verifico se sono in attesa del comando inviato.
                                if (this.waitingEcho)
                                {
                                    //Verifico se è scaduto il timeout di attesa.
                                    if (Environment.TickCount - this.waitingTimeout > 0)
                                    {
                                        this.waitingEcho = false;
                                    }
                                    else
                                    {
                                        //Cerco il comando inviato.
                                        this.frameIndex = ATParser.IndexOfSequence(this.receivingBuffer, this.receivingBufferIndex, this.receivingBufferCount, this.sendingBuffer, 0, this.sendingLength, true);

                                        //Verifico se ho trovato il comando inviato.
                                        if (this.frameIndex != -1)
                                        {
                                            this.waitingEcho = false;
                                            this.waitingEchoEvent.Set();

                                            //Cancello il contenuto del buffer relativo al comando inviato.
                                            movingBytes = this.receivingBufferIndex + this.receivingBufferCount -
                                                this.frameIndex - this.sendingLength;

                                            if (movingBytes > 0)
                                            {
                                                Array.Copy(this.receivingBuffer, this.receivingBufferIndex + this.receivingBufferCount -
                                                    movingBytes, this.receivingBuffer, this.frameIndex, movingBytes);
                                            }

                                            this.receivingBufferCount -= this.sendingLength;
                                        }
                                    }
                                }

                                //Verifico se sono in attesa di una risposta attesa.
                                if (this.receivingBufferCount > 0 && this.waitingResponse && !this.waitingEcho)
                                {
                                    //Verifico se è scaduto il timeout di attesa.
                                    if (Environment.TickCount - this.waitingTimeout > 0)
                                    {
                                        this.waitingResponse = false;

                                        //Verifico se bisogna invocare una callback.
                                        if (this.waitingCallback != null)
                                        {
                                            this.waitingAsyncResult.AsyncException =
                                                new ATModemException(ATModemError.Timeout);

                                            this.waitingCallback(this.waitingAsyncResult);
                                        }
                                    }
                                    else
                                    {
                                        //Eseguo il parse della risposta attesa.
                                        this.parserResult = this.parser.ParseResponse(this.waitingCommand,
                                            this.waitingCommandType, this.receivingBuffer,
                                            this.receivingBufferIndex, this.receivingBufferCount);

                                        if (this.parserResult != null && parserResult.Success)
                                        {
#if (DEBUG)
                                            Debug.Print("ParserResult.Success > " + this.parserResult.Command);
#endif

                                            this.dataLength = this.parserResult.DataLength;
                                            this.dataStream = this.dataLength > 0 ? ATResponseDataStream.GetInstance(this.dataLength) : null;

                                            //Notifico la ricezione della risposta attesa.
                                            this.waitingResponse = false;
                                            this.waitingFrame = ATFrame.GetInstance(parserResult.Command, parserResult.CommandType, this.dataStream, parserResult.Unsolicited, parserResult.Result, parserResult.OutParameters);
                                            this.waitingResponseEvent.Set();

                                            //Verifico se bisogna invocare una callback.
                                            if (this.waitingCallback != null)
                                            {
                                                this.waitingCallback(this.waitingAsyncResult);
                                            }
                                        }
                                    }
                                }

                                if (this.receivingBufferCount > 0 && (this.parserResult == null || !parserResult.Success))
                                {
                                    //Eseguo il parse della risposta non attesa.
                                    this.parserResult = this.parser.ParseUnsolicitedResponse(this.receivingBuffer,
                                        this.receivingBufferIndex, this.receivingBufferCount);

                                    if (this.parserResult != null && parserResult.Success)
                                    {
#if (DEBUG)
                                        Debug.Print("ParserUnsolicitedResult.Success > " + this.parserResult.Command);
#endif

                                        this.dataLength = this.parserResult.DataLength;
                                        this.dataStream = this.dataLength > 0 ? ATResponseDataStream.GetInstance(this.dataLength) : null;

                                        //Notifico la ricezione della risposta non attesa.
                                        if (this.FrameReceived != null)
                                            this.FrameReceived(this, ATModemFrameEventArgs.GetInstance(ATFrame.GetInstance(parserResult.Command, parserResult.CommandType, this.dataStream, parserResult.Unsolicited, parserResult.Result, parserResult.OutParameters)));
                                    }
                                }

                                if (this.parserResult != null && parserResult.Success)
                                {
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

                                                lock (this.waitingLock)
                                                {
                                                    //Verifico se sono in attesa di una risposta o se è scaduto il data timeout.
                                                    if (this.dataTime > defaultDataTimeout)
                                                    {
                                                        this.dataStream.Close();

                                                        this.readDataLength = 0;

                                                        break;
                                                    }
                                                }

                                                this.readDataLength = this.dataLength - this.dataCount;

                                                if (this.readDataLength > readBytes)
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

                                    if (!this.waitingResponse)
                                    {
                                        //Cerco il primo delimitatore.
                                        firstDelimiterIndex = this.parser.IndexOfDelimitor(this.receivingBuffer,
                                            this.receivingBufferIndex, this.receivingBufferCount, true);

                                        if (firstDelimiterIndex != -1)
                                        {
                                            secondDelimiterIndex = firstDelimiterIndex + this.parser.LengthOfDelimitor();

                                            //Cerco il secondo delimitatore.
                                            secondDelimiterIndex = this.parser.IndexOfDelimitor(this.receivingBuffer,
                                                secondDelimiterIndex, this.receivingBufferCount +
                                                this.receivingBufferIndex - secondDelimiterIndex, true);

                                            if (secondDelimiterIndex != -1)
                                            {
                                                if (firstDelimiterIndex == this.receivingBufferIndex)
                                                {
                                                    firstDelimiterIndex += this.parser.LengthOfDelimitor();
                                                }

                                                //Cancello il contenuto del buffer fino al primo delimitatore.
                                                this.receivingBufferCount -= (firstDelimiterIndex - this.receivingBufferIndex);
                                                this.receivingBufferIndex = firstDelimiterIndex;

                                                //Continuo l'elaborazione del buffer.
                                                continue;
                                            }
                                        }
                                    }

                                    //Forzo il caricamento del buffer per evitare cicli infiniti.
                                    break;
                                }
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
    }
}
