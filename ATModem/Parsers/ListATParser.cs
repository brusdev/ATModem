using System;
using BrusDev.IO.Modems.Parsers;
using System.Collections;
using BrusDev.Text.RegularExpressions;
using BrusDev.IO.Modems.Frames;

namespace BrusDev.IO.Modems.Parsers
{
    public abstract class BaseATParser : ATParser
    {
        private Hashtable frameParsers;
        private ArrayList unsolicitedFrameParsers;
        private byte[] delimitorSequence;
        private int delimitorSequenceLength;

        protected Regex simpleResponseRegex;

        protected BaseATParser(byte[] delimitorSequence)
        {
            this.simpleResponseRegex = new Regex(@"\r\n(OK|ERROR)\r\n");

            this.frameParsers = new Hashtable();
            this.unsolicitedFrameParsers = new ArrayList();
            this.delimitorSequence = delimitorSequence;
            this.delimitorSequenceLength = this.delimitorSequence.Length;
        }

        public override ATParserResult ParseResponse(string command, ATCommandType commandType, byte[] buffer, int index, int count)
        {
            ATFrameParser frameParser = (ATFrameParser)this.frameParsers[
                this.GetFrameParserKey(command, commandType)];


            if (frameParser == null)
                return null;

            return frameParser.Parse(buffer, index, count);
        }

        public override ATParserResult ParseUnsolicitedResponse(byte[] buffer, int index, int count)
        {
            ATParserResult parserResult;


            foreach (ATFrameParser frameParser in this.unsolicitedFrameParsers)
            {
                parserResult = frameParser.Parse(buffer, index, count);

                if (parserResult != null && parserResult.Success)
                    return parserResult;
            }

            return null;
        }

        public override int IndexOfDelimitor(byte[] buffer, int index, int count, bool ignoreTruncated)
        {
            return IndexOfSequence(buffer, index, count, this.delimitorSequence, 0, this.delimitorSequence.Length, ignoreTruncated);
        }


        private string GetFrameParserKey(string command, ATCommandType commandType)
        {
            return String.Concat(commandType, "+", command);
        }

        protected void AddRegexFrameParser(string command, ATCommandType commandType, Regex regex, int regexParametersGroup, int regexResultGroup)
        {
            this.AddFrameParser(new RegexATFrameParser()
            {
                Command = command,
                CommandType = commandType,
                Regex = regex,
                RegexParametersGroup = regexParametersGroup,
                RegexResultGroup = regexResultGroup
            });
        }

        protected void AddFrameParser(ATFrameParser frameParser)
        {
            this.frameParsers.Add(this.GetFrameParserKey(frameParser.Command, frameParser.CommandType), frameParser);
        }

        protected void AddUnsolicitedFrameParser(ATFrameParser frameParser)
        {
            this.unsolicitedFrameParsers.Add(frameParser);
        }

        public override int LengthOfDelimitor()
        {
            return this.delimitorSequenceLength;
        }
    }
}
