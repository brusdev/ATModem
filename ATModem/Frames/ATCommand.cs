using System;

namespace BrusDev.IO.Modems.Frames
{
    public class ATCommand
    {
        public const string AT = "AT";
        public const string ATE0 = "ATE0";
        public const string ATE1 = "ATE1";
        public const string AT_CCWA = "AT+CCWA";
        public const string AT_CIPHEAD = "AT+CIPHEAD";
        public const string AT_CIPMODE = "AT+CIPMODE";
        public const string AT_CIPMUX = "AT+CIPMUX";
        public const string AT_CIPSPRT = "AT+CIPSPRT";
        public const string AT_CSTT = "AT+CSTT";
        public const string AT_CDNSCFG = "AT+CDNSCFG";
        public const string AT_CDNSGIP = "AT+CDNSGIP";
        public const string AT_CIICR = "AT+CIICR";
        public const string AT_CIFSR = "AT+CIFSR";
        public const string AT_CIPSTART = "AT+CIPSTART";
        public const string AT_CIPSEND = "AT+CIPSEND";
        public const string AT_CIPCLOSE = "AT+CIPCLOSE";
        public const string AT_CIPSERVER = "AT+CIPSERVER";
        public const string AT_CPIN = "AT+CPIN";
        public const string AT_CSQ = "AT+CSQ";
        public const string AT_CREG = "AT+CREG";
        public const string AT_CGREG = "AT+CGREG";
        public const string AT_CGATT = "AT+CGATT";
        public const string AT_GSN = "AT+GSN";
        public const string AT_CIPSHUT = "AT+CIPSHUT";
        public const string AT_CIPSTATUS = "AT+CIPSTATUS";

        public const string CONNECT = "CONNECT";
        public const string CDNSGIP = "CDNSGIP";
        public const string CREG = "CREG";
        public const string CGREG = "CGREG";
        public const string IPD = "IPD";
        public const string SEND = "SEND";
        public const string NORMAL_POWER_DOWN = "NORMAL POWER DOWN";
        public const string SEND_PROMPT = "SEND PROMPT";
        public const string CLOSED = "CLOSED";
        public const string REMOTE_IP = "REMOTE IP";
        public const string RECEIVE = "RECEIVE";


        /***ESP8266***/
        public const string AT_GMR = "AT+GMR";
        public const string AT_RST = "AT+RST";
        public const string AT_CWMODE = "AT+CWMODE";
        public const string AT_CWJAP = "AT+CWJAP";
        public const string AT_CWQAP = "AT+CWQAP";

    }
}
