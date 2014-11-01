using BrusDev.IO.Modems.Frames;
using System;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public abstract class ATFrameParser
    {
        public string Command { get; set; }
        public ATCommandType CommandType { get; set; }
        public bool Unsolicited { get; set; }

        public abstract ATParserResult Parse(byte[] buffer, int index, int count);
    }
}
