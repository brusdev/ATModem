using BrusDev.IO.Modems.Frames;
using System;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public abstract class ATParser
    {
        public abstract ATParserResult ParseResponse(string command, ATCommandType commandType, byte[] buffer, int index, int count);

        public abstract ATParserResult ParseUnsolicitedResponse(byte[] buffer, int index, int count);

        public abstract int IndexOfDelimitor(byte[] buffer, int index, int count, bool ignoreTruncated);

        public abstract int LengthOfDelimitor();

        public static int CompareSequence(byte[] sourceArray, int sourceIndex, int sourceLength, byte[] sequenceArray, int sequenceIndex, int sequenceLength)
        {
            int compareIndex = 0;

            if (sourceLength != sequenceLength)
                return sourceLength - sequenceLength;

            while (compareIndex < sequenceLength && sourceArray[sourceIndex + compareIndex] ==
                sequenceArray[sequenceIndex + compareIndex])
                compareIndex++;

            return compareIndex - sequenceLength;
        }

        public static int IndexOfSequence(byte[] sourceArray, int sourceIndex, int sourceLength, byte[] sequenceArray, int sequenceIndex, int sequenceLength, bool ignoreTruncated)
        {
            int baseIndex = 0;
            int compareIndex;

            while (baseIndex < sourceLength && sourceLength - baseIndex >= sequenceLength)
            {
                compareIndex = 0;

                while (compareIndex < sequenceLength && sequenceArray[sequenceIndex + compareIndex] ==
                    sourceArray[sourceIndex + baseIndex + compareIndex])
                    compareIndex++;

                if (compareIndex == sequenceLength)
                    return sourceIndex + baseIndex;

                baseIndex++;
            }

            if (!ignoreTruncated)
            {
                while (baseIndex < sourceLength)
                {
                    compareIndex = 0;

                    while (compareIndex < sequenceLength && baseIndex + compareIndex < sourceLength &&
                        sequenceArray[sequenceIndex + compareIndex] == sourceArray[sourceIndex + baseIndex + compareIndex])
                        compareIndex++;

                    if (compareIndex == sourceLength - baseIndex)
                        return sourceIndex + baseIndex;

                    baseIndex++;
                }
            }

            return -1;
        }

        /// <summary>
        /// Converts a byte array to a char array
        /// </summary>
        /// <param name="Input">The byte array</param>
        /// <returns>The char array</returns>
        public static char[] Bytes2Chars(byte[] bytes, int index, int count)
        {
            char[] chars = new char[count];

            for (int i = 0; i < count; i++)
                chars[i] = (char)bytes[index + i];

            return chars;
        }

        /// <summary>
        /// Converts a char array to a byte array
        /// </summary>
        /// <param name="Input">The char array</param>
        /// <returns>The byte array</returns>
        public static byte[] Chars2Bytes(char[] chars, int index, int count)
        {
            byte[] bytes = new byte[count];

            for (int i = 0; i < count; i++)
                bytes[i] = (byte)chars[index + i];

            return bytes;
        }
    }
}
