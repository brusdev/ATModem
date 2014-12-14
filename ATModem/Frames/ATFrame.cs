using System;
using System.Text;

namespace BrusDev.IO.Modems.Frames
{
    public class ATFrame
    {
        private const byte byte_EqualSign = (byte)'=';
        private const byte byte_QuestionMark = (byte)'?';
        private const byte byte_CarriageReturn = (byte)'\r';
        private static StringBuilder commandStringBuilder = new StringBuilder();

        public static ATFrame Instance = new ATFrame();


        public string Command { get; set; }
        public ATCommandType CommandType { get; set; }
        public string InParameters { get; set; }

        public bool Unsolicited { get; set; }
        public string Result { get; set; }
        public string OutParameters { get; set; }
        public int DataLength { get; set; }

        public int GetBytes(byte[] bytes, int byteIndex)
        {
            lock (commandStringBuilder)
            {
                int index = byteIndex;
                int length = this.Command.Length;


                for (int i = 0; i < length; i++)
                    bytes[index++] = (byte)this.Command[i];

                switch (this.CommandType)
                {
                    case ATCommandType.Test:
                        bytes[index++] = byte_EqualSign;
                        bytes[index++] = byte_QuestionMark;
                        break;
                    case ATCommandType.Read:
                        bytes[index++] = byte_QuestionMark;
                        break;
                    case ATCommandType.Write:
                        bytes[index++] = byte_EqualSign;

                        length = this.InParameters.Length;

                        for (int i = 0; i < length; i++)
                            bytes[index++] = (byte)this.InParameters[i];

                        break;
                }

                bytes[index++] = byte_CarriageReturn;

                return index - byteIndex;
            }
        }
    }
}
