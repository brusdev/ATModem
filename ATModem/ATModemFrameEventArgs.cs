using BrusDev.IO.Modems.Frames;
using System;

namespace BrusDev.IO.Modems
{
    public class ATModemFrameEventArgs
    {
        private static ATModemFrameEventArgs instance = new ATModemFrameEventArgs(null);

        private ATFrame frame;


        public ATFrame Frame { get { return this.frame; } }


        public ATModemFrameEventArgs(ATFrame frame)
        {
            this.frame = frame;
        }

        public static ATModemFrameEventArgs GetInstance(ATFrame frame)
        {
            instance.frame = frame;
            return instance;
        }
    }
}
