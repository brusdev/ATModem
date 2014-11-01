using System;

namespace BrusDev.IO.Modems
{
    public class ATModemDataEventArgs
    {
        private byte[] data;


        public byte[] Data { get { return this.data; } }


        public ATModemDataEventArgs(byte[] data)
        {
            this.data = data;
        }
    }
}
