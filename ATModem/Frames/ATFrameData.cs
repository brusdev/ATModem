using System;

using BrusDev.IO.Modems.Frames;

namespace BrusDev.IO.Modems.Frames
{
    public class ATFrameData
    {
        private const int bufferSize = 128;

        private static ATFrameData instance = new ATFrameData(null);


        private int free;
        private int head;
        private int rightHead;
        private int rightTail;
        private int tail;
        private int used;
        private byte[] buffer;
        private ATFrame frame;

        public ATFrame Frame { get { return this.frame; } }
        public int Available { get { return this.used; } }

        public ATFrameData(ATFrame frame)
        {
            this.frame = frame;

            this.buffer = new byte[bufferSize];

            this.Clear();
        }

        public void Clear()
        {
            lock (this.buffer)
            {
                this.free = bufferSize;
                this.head = 0;
                this.rightTail = 0;
                this.rightHead = 0;
                this.tail = 0;
                this.used = 0;
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            lock (this.buffer)
            {
                if (this.used == 0)
                    return 0;

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

                return count;
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            lock (this.buffer)
            {
                if (count > this.free)
                    count = this.free;

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
            }
        }


        public static ATFrameData GetInstance(ATFrame frame)
        {
            instance.frame = frame;

            instance.Clear();

            return instance;
        }
    }
}
