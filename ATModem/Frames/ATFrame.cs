#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif
using System;
using System.IO;
using System.Text;

namespace BrusDev.IO.Modems.Frames
{
    public class ATFrame
    {
        private const byte byte_EqualSign = (byte)'=';
        private const byte byte_QuestionMark = (byte)'?';
        private const byte byte_CarriageReturn = (byte)'\r';
        private const byte byte_LineFeed = (byte)'\n';
        private static StringBuilder commandStringBuilder = new StringBuilder();

        private static ATFrame requestInstance = new ATFrame();
        private static ATFrame responseInstance = new ATFrame();
        private static ATFrame unsolicitedResponseInstance = new ATFrame();

        private string command;
        private ATCommandType commandType;
        private Stream dataStream;

        private string inParameters;

        private bool unsolicited;
        private string result;
        private string outParameters;



        public string Command { get { return this.command; } }
        public ATCommandType CommandType { get { return this.commandType; } }
        public string InParameters { get { return this.inParameters; } }

        public bool Unsolicited { get { return this.unsolicited; } }
        public string Result { get { return this.result; } }
        public string OutParameters { get { return this.outParameters; } }

        public Stream DataStream { get { return this.dataStream; } }


        private ATFrame()
        {
        }

        public ATFrame(string command, ATCommandType commandType, Stream dataStream, string inParameters)
        {
            this.command = command;
            this.commandType = commandType;
            this.dataStream = dataStream;
            this.inParameters = inParameters;
        }

        public ATFrame(string command, ATCommandType commandType, Stream dataStream, bool unsolicited, string result, string outParameters)
        {
            this.command = command;
            this.commandType = commandType;
            this.dataStream = dataStream;
            this.unsolicited = unsolicited;
            this.result = result;
            this.outParameters = outParameters;
        }

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
                bytes[index++] = byte_LineFeed;

                return index - byteIndex;
            }
        }

        public static ATFrame GetInstance(string command, ATCommandType commandType, Stream dataStream, string inParameters)
        {
            ATFrame instance = requestInstance;

            instance.command = command;
            instance.commandType = commandType;
            instance.dataStream = dataStream;

            instance.inParameters = inParameters;

            return instance;
        }

        public static ATFrame GetInstance(string command, ATCommandType commandType, Stream dataStream, bool unsolicited, string result, string outParameters)
        {
            ATFrame instance = unsolicited ? unsolicitedResponseInstance : responseInstance;

            instance.command = command;
            instance.commandType = commandType;
            instance.dataStream = dataStream;

            instance.unsolicited = unsolicited;
            instance.result = result;
            instance.outParameters = outParameters;

            return instance;
        }
    }
}
