using BrusDev.IO.Modems.Frames;
using System;

namespace BrusDev.IO.Modems
{
    public class ATModemFrameEventArgs
    {
        private ATFrame frame;


        public ATFrame Frame { get { return this.frame; } }


        public ATModemFrameEventArgs(ATFrame frame)
        {
            this.frame = frame;
        }
    }
}
