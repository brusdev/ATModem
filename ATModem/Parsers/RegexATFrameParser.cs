﻿using BrusDev.IO.Modems.Frames;
using BrusDev.Text.RegularExpressions;
using System;
using System.Text;

namespace BrusDev.IO.Modems.Parsers
{
    public class RegexATFrameParser : ATFrameParser
    {
        public Regex Regex { get; set; }
        public int RegexParametersGroup { get; set; }
        public int RegexResultGroup { get; set; }
        public int RegexDataLengthGroup { get; set; }

        public RegexATFrameParser()
        {
            this.Unsolicited = false;
        }

        public override ATParserResult Parse(byte[] buffer, int index, int count)
        {
            Match responseMatch;
            ATParserResult parserResult = null;

            responseMatch = this.Regex.Match(buffer, index, count);

            if (responseMatch.Success)
            {
                parserResult = this.Unsolicited ? ATParserResult.UnsolicitedInstance : ATParserResult.Instance;
                parserResult.Command = this.Command;
                parserResult.CommandType = this.CommandType;
                parserResult.Unsolicited = this.Unsolicited;
                parserResult.Success = true;
                parserResult.Index = responseMatch.Index;
                parserResult.Length = responseMatch.Length;

                if (this.RegexResultGroup > 0 &&
                    this.RegexResultGroup < responseMatch.Groups.Length &&
                    responseMatch.Groups[this.RegexResultGroup].Success)
                {
                    parserResult.Result = this.GetText(buffer, responseMatch.Groups[this.RegexResultGroup].index, responseMatch.Groups[this.RegexResultGroup].length);
                }
                else
                {
                    parserResult.Result = null;
                }

                if (this.RegexParametersGroup > 0 &&
                    this.RegexParametersGroup < responseMatch.Groups.Length &&
                    responseMatch.Groups[this.RegexParametersGroup].Success)
                {
                    parserResult.OutParameters = this.GetText(buffer, responseMatch.Groups[this.RegexParametersGroup].index, responseMatch.Groups[this.RegexParametersGroup].length);
                }
                else
                {
                    parserResult.OutParameters = null;
                }

                if (this.RegexDataLengthGroup > 0 &&
                    this.RegexDataLengthGroup < responseMatch.Groups.Length &&
                    responseMatch.Groups[this.RegexDataLengthGroup].Success)
                {
                    parserResult.DataLength = Convert.ToInt32(this.GetText(buffer, responseMatch.Groups[this.RegexDataLengthGroup].index, responseMatch.Groups[this.RegexDataLengthGroup].length));
                }
                else
                {
                    parserResult.DataLength = 0;
                }
            }

            return parserResult;
        }

        private string GetText(byte[] bytes, int index, int count)
        {
            char[] chars = new char[count];

            for (int i = 0; i < count; i++)
                chars[i] = (char)bytes[index + i];

            return new string(chars);
        }
    }
}
