using BrusDev.IO.Modems.Frames;
using System;

namespace BrusDev.IO.Modems
{
    public class ATModemFrameDataEventArgs
    {
        private static ATModemFrameDataEventArgs instance = new ATModemFrameDataEventArgs(null);

        private ATFrameData frameData;


        public ATFrameData FrameData { get { return this.frameData; } }


        public ATModemFrameDataEventArgs(ATFrameData frameData)
        {
            this.frameData = frameData;
        }

        public static ATModemFrameDataEventArgs GetInstance(ATFrameData frameData)
        {
            instance.frameData = frameData;
            return instance;
        }
    }
}
