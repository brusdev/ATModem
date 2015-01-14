using BrusDev.IO.Modems.Frames;
using BrusDev.Text.RegularExpressions;
using System;
using System.Collections;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public class ESP8266ATParser : BaseATParser
    {
        private static ESP8266ATParser instance = new ESP8266ATParser();

        public static ESP8266ATParser GetInstance()
        {
            return instance;
        }


        public ESP8266ATParser()
            : base(new byte[] { (byte)'\r', (byte)'\n' })
        {
            #region Initialize frameParsers...

            this.AddRegexFrameParser(ATCommand.AT, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.ATE0, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.ATE1, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPMODE, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPMUX, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CSTT, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIFSR, Frames.ATCommandType.Execution,
                new Regex(@"\r\n([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})\r\n|\r\n(ERROR)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CIPSTART, Frames.ATCommandType.Write,
                new Regex(@"\r\n(OK|ERROR|\+CME ERROR: [0-9]+|ALREADY CONNECT)\r\n"), 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPCLOSE, Frames.ATCommandType.Write,
                new Regex(@"\r\n(OK)\r\n|link is not\r\n"), 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPSHUT, Frames.ATCommandType.Execution,
                new Regex(@"\r\nSHUT OK\r\n|\r\n(ERROR)\r\n"), 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPSTATUS, Frames.ATCommandType.Execution,
                new Regex(@"STATUS:([^\r\n]+)\r\n\r\n(OK)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CIPSERVER, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CIPSEND, Frames.ATCommandType.Write,
                new Regex(@"> |\r\nERROR\r\n"), 0, 0);

            /***ESP8266***/
            //
            this.AddRegexFrameParser(ATCommand.AT_GMR, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_RST, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CWMODE, Frames.ATCommandType.Write,
                new Regex(@"\r\n(OK|no change|ERROR)\r\n"), 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CWJAP, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            this.AddRegexFrameParser(ATCommand.AT_CWQAP, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);

            
            #endregion


            #region Initialize unsolicitedFrameParsers...

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CONNECT,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\nLinked\r\n"),
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.IPD,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n\+IPD,([0-9]+),([0-9]+):"),
                RegexDataLengthGroup = 2,
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.SEND,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\nSEND (OK|FAIL)\r\n"),
                RegexParametersGroup = 0,
                RegexResultGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CLOSED,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\nUnlink\r\n"),
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.REMOTE_IP,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n([0-9]),REMOTE IP: ([^\r]+)\r\n"),
                RegexParametersGroup = 1,
                RegexResultGroup = 2
            });

            #endregion
        }
    }
}
