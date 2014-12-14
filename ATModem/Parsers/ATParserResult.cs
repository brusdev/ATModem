using BrusDev.IO.Modems.Frames;
using System;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public class ATParserResult
    {
        public static ATParserResult Instance = new ATParserResult();
        public static ATParserResult UnsolicitedInstance = new ATParserResult();

        public bool Success { get; set; }
        public ATFrame Frame { get; set; }
        public int Index { get; set; }
        public int Length { get; set; }
    }
}
