using BrusDev.IO.Modems.Frames;
using BrusDev.Text.RegularExpressions;
using System;
using System.Collections;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public class SIM900ATParser : ATParser
    {
        private Hashtable frameParsers;
        private ArrayList unsolicitedFrameParsers;
        private byte[] delimitorSequence;

        public SIM900ATParser()
        {
            Regex simpleResponseRegex = new Regex(@"\r\n(OK|ERROR)\r\n");


            this.frameParsers = new Hashtable();
            this.unsolicitedFrameParsers = new ArrayList();
            this.delimitorSequence = new byte[] { (byte)'\r', (byte)'\n' };


            #region Initialize frameParsers...

            this.AddRegexFrameParser(ATCommand.ATE0, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.ATE1, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPHEAD, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPMODE, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPMUX, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPSPRT, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CSTT, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CDNSCFG, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CDNSGIP, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIICR, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIFSR, Frames.ATCommandType.Execution,
                new Regex(@"\r\n([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})\r\n|\r\n(ERROR)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CIPSTART, Frames.ATCommandType.Write,
                new Regex(@"\r\n(OK|ERROR|\+CME ERROR: [0-9]+|ALREADY CONNECT)\r\n"), 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPCLOSE, Frames.ATCommandType.Execution,
                new Regex(@"\r\n(CLOSE OK)|(ERROR)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CPIN, Frames.ATCommandType.Read,
                new Regex(@"\r\n\+CPIN: ([^\r\n]+)\r\n\r\n(OK)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CSQ, Frames.ATCommandType.Execution,
                new Regex(@"\r\n\+CSQ: ([^\r\n]+)\r\n\r\n(OK|\+CME ERROR: [0-9]+)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CREG, Frames.ATCommandType.Read,
                new Regex(@"\r\n\+CREG: ([^\r\n]+)\r\n\r\n(OK|\+CME ERROR: [0-9]+)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CGREG, Frames.ATCommandType.Read,
                new Regex(@"\r\n\+CGREG: ([^\r\n]+)\r\n\r\n(OK|\+CME ERROR: [0-9]+)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CGATT, Frames.ATCommandType.Read,
                new Regex(@"\r\n\+CGATT: ([^\r\n]+)\r\n\r\n(OK|ERROR)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_GSN, Frames.ATCommandType.Read,
                new Regex(@"\r\n([^\r\n]+)\r\n\r\n(OK|ERROR)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CCWA, Frames.ATCommandType.Read,
                new Regex(@"\r\n\+CCWA: (0|1)\r\n\r\n(OK)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CIPSHUT, Frames.ATCommandType.Execution,
                new Regex(@"\r\nSHUT OK\r\n|\r\n(ERROR)\r\n"), 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPSTATUS, Frames.ATCommandType.Execution,
                new Regex(@"\r\n(OK)\r\n\r\nSTATE: ([^\r\n]+)\r\n"), 2, 1);
            
            #endregion


            #region Initialize unsolicitedFrameParsers...

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CONNECT,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\nCONNECT (OK)\r\n"),
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CDNSGIP,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n\+CDNSGIP: ([^\r\n]+)\r\n"),
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CREG,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n\+CREG: ([0-9]+)\r\n"),
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CGREG,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n\+CGREG: ([0-9]+)\r\n"),
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.AT_CCWA,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n\+CCWAA: (0|1)\r\n"),
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.IPD,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n\+IPD,([0-9]+):"),
                RegexDataLengthGroup = 1,
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.SEND,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\nSEND (OK|FAIL)\r\n"),
                RegexResultGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.NORMAL_POWER_DOWN,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\nNORMAL POWER DOWN\r\n"),
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.SEND_PROMPT,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n> "),
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CLOSED,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\nCLOSED\r\n"),
            });

            #endregion
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
    }
}
