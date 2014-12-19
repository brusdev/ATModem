using BrusDev.IO.Modems.Frames;
using System;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public class ATParserResult
    {
        public static ATParserResult Instance = new ATParserResult();
        public static ATParserResult UnsolicitedInstance = new ATParserResult();

        public string Command { get; set; }
        public ATCommandType CommandType { get; set; }
        public bool Unsolicited { get; set; }
        public bool Success { get; set; }
        public int Index { get; set; }
        public int Length { get; set; }
        public string OutParameters { get; set; }
        public string Result { get; set; }
        public int DataLength { get; set; }
    }
}
