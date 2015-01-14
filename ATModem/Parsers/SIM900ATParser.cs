using BrusDev.IO.Modems.Frames;
using BrusDev.Text.RegularExpressions;
using System;
using System.Collections;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public class SIM900ATParser : BaseATParser
    {
        private static SIM900ATParser instance = new SIM900ATParser();

        public static SIM900ATParser GetInstance()
        {
            return instance;
        }

        public SIM900ATParser()
            : base(new byte[] { (byte)'\r', (byte)'\n' })
        {
            #region Initialize frameParsers...

            this.AddRegexFrameParser(ATCommand.AT, Frames.ATCommandType.Execution,
                simpleResponseRegex, 0, 1);
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
            this.AddRegexFrameParser(ATCommand.AT_CIPCLOSE, Frames.ATCommandType.Write,
                new Regex(@"\r\n([0-9]),CLOSE OK\r\n|\r\n(ERROR)\r\n"), 1, 2);
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
            this.AddRegexFrameParser(ATCommand.AT_CIPSTATUS, Frames.ATCommandType.Write,
                new Regex(@"\r\n\+CIPSTATUS: ([^\r\n]+)\r\n\r\n(OK)\r\n"), 1, 2);
            this.AddRegexFrameParser(ATCommand.AT_CIPSERVER, Frames.ATCommandType.Write,
                simpleResponseRegex, 0, 1);
            
            #endregion


            #region Initialize unsolicitedFrameParsers...

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.CONNECT,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n([0-9]), CONNECT OK\r\n"),
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
                Regex = new Regex(@"^\r\n([0-9]), SEND (OK|FAIL)\r\n"),
                RegexParametersGroup = 1,
                RegexResultGroup = 2
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
                Regex = new Regex(@"^\r\n([0-9]), CLOSED\r\n"),
                RegexParametersGroup = 1
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.REMOTE_IP,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n([0-9]), REMOTE IP: ([^\r]+)\r\n"),
                RegexParametersGroup = 1,
                RegexResultGroup = 2
            });

            this.AddUnsolicitedFrameParser(new RegexATFrameParser()
            {
                Command = ATCommand.RECEIVE,
                CommandType = Frames.ATCommandType.Execution,
                Unsolicited = true,
                Regex = new Regex(@"^\r\n\+RECEIVE,([0-9]),([0-9]+):"),
                RegexParametersGroup = 1,
                RegexDataLengthGroup = 2,
            });

            #endregion
        }
    }
}
