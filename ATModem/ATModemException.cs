using BrusDev.IO.Modems.Frames;
using System;
using System.Text;

namespace BrusDev.IO.Modems
{
    public class ATModemException : Exception
    {
        public ATModemError Error { get; private set; }

        public ATModemException(ATModemError error)
        {
            this.Error = error;
        }

        public ATModemException(ATModemError error, string message, Exception innerException)
            : base(message, innerException)
        {
            this.Error = error;
        }
    }
}
