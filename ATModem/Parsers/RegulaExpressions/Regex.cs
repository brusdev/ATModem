using System;
using System.Collections;
using System.Text;

namespace BrusDev.Text.RegularExpressions
{
    public class Regex
    {
        private const char char_0 = '0';
        private const char char_9 = '9';
        private const char char_A = 'A';
        private const char char_Z = 'Z';
        private const char char_a = 'a';
        private const char char_z = 'z';
        private const char char_r = 'r';
        private const char char_n = 'n';
        private const char char_d = 'd';
        private const char char_w = 'w';
        private const char char_s = 's';
        private const char char_t = 't';
        private const char char_Space = ' ';
        private const char char_Tab = '\t';
        private const char char_Comma = ',';
        private const char char_Asterisk = '*';
        private const char char_PlusSign = '+';
        private const char char_MinusSign = '-';
        private const char char_VerticalLine = '|';
        private const char char_LeftParenthesis = '(';
        private const char char_RightParenthesis = ')';
        private const char char_CircumflexAccent = '^';
        private const char char_LeftSquareBracket = '[';
        private const char char_RightSquareBracket = ']';
        private const char char_LeftCurlyBracket = '{';
        private const char char_RightCurlyBracket = '}';
        private const char char_CarriageReturn = '\r';
        private const char char_LineFeed = '\n';
        private const char char_BackSlash = '\\';

        private const byte symbol_Char = 0x00;
        private const byte symbol_Class = 0x01;
        private const byte symbol_NegatedClass = 0x02;
        private const byte symbol_Digit = 0x03;
        private const byte symbol_WordCharacter = 0x04;


        private char[] patternChars;


        #region CharByteList...

        private class CharByteList : IList
        {
            private IList list;

            public CharByteList(IList list)
            {
                this.list = list;
            }

            public int Add(object value)
            {
                return this.list.Add(value);
            }

            public void Clear()
            {
                this.list.Clear();
            }

            public bool Contains(object value)
            {
                return this.list.Contains(value);
            }

            public int IndexOf(object value)
            {
                return this.list.IndexOf(value);
            }

            public void Insert(int index, object value)
            {
                this.list.Insert(index, value);
            }

            public bool IsFixedSize
            {
                get { return this.list.IsFixedSize; }
            }

            public bool IsReadOnly
            {
                get { return this.list.IsReadOnly; }
            }

            public void Remove(object value)
            {
                this.list.Remove(value);
            }

            public void RemoveAt(int index)
            {
                this.list.Remove(index);
            }

            public object this[int index]
            {
                get
                {
                    return (char)(byte)this.list[index];
                }
                set
                {
                    this.list[index] = value;
                }
            }

            public void CopyTo(Array array, int index)
            {
                this.list.CopyTo(array, index);
            }

            public int Count
            {
                get { return this.list.Count; }
            }

            public bool IsSynchronized
            {
                get { return this.list.IsSynchronized; }
            }

            public object SyncRoot
            {
                get { return this.list.SyncRoot; }
            }

            public IEnumerator GetEnumerator()
            {
                return this.list.GetEnumerator();
            }
        }

        #endregion


        public Regex(string pattern)
            : this(pattern.ToCharArray())
        {
        }

        public Regex(char[] patternBytes)
        {
            this.patternChars = patternBytes;
        }

        public Match Match(string search)
        {
            return this.Match(search.ToCharArray(), 0, search.Length);
        }

        public Match Match(IList searchList, int searchIndex, int searchLength)
        {
            char searchingChar;
            int searchingGroups;
            bool searchingEscaped;
            bool searchingCondition;
            char searchingNextChar;
            char searchingPreviousChar;

            int patternIndex = 0;
            byte patternSymbol = symbol_Char;
            int patternLength = this.patternChars.Length;
            char patternChar = this.patternChars[patternIndex];

            bool escaped = false;
            bool comparing = false;
            bool startAnchored = false;
            int repetitions = 0;
            int minRepetitions = 1;
            int maxRepetitions = 1;
            ArrayList classChars = new ArrayList();

            Match match;
            Group group;
            Stack groupStack = new Stack();
            ArrayList groupList = new ArrayList();


            match = new Match() { index = searchIndex, patternBeginIndex = patternIndex, success = true };

            group = match;
            groupList.Add(group);

            //Check if searchList is byte list.
            if (searchList[searchIndex] is byte)
                searchList = new CharByteList(searchList);

            searchLength += searchIndex;

            while (true)
            {
                while (patternIndex < patternLength)
                {
                    if (escaped)
                    {
                        escaped = false;
                        comparing = true;

                        switch (patternChar)
                        {
                            case char_d:
                                patternSymbol = symbol_Digit;
                                break;
                            case char_w:
                                patternSymbol = symbol_WordCharacter;
                                break;
                            case char_s:
                                patternChar = char_Space;
                                patternSymbol = symbol_Char;
                                break;
                            case char_r:
                                patternChar = char_CarriageReturn;
                                patternSymbol = symbol_Char;
                                break;
                            case char_n:
                                patternChar = char_LineFeed;
                                patternSymbol = symbol_Char;
                                break;
                            case char_t:
                                patternChar = char_Tab;
                                patternSymbol = symbol_Char;
                                break;
                            default:
                                patternSymbol = symbol_Char;
                                break;
                        }
                    }
                    else
                    {
                        switch (patternChar)
                        {
                            case char_BackSlash:
                                escaped = true;
                                break;
                            case char_CircumflexAccent:
                                startAnchored = true;
                                break;
                            case char_VerticalLine:
                                if (group.success)
                                {
                                    searchingGroups = 0;
                                    searchingEscaped = false;

                                    //Search group end.
                                    while (patternIndex < patternLength)
                                    {
                                        searchingNextChar = this.patternChars[patternIndex];

                                        if (searchingEscaped)
                                        {
                                            searchingEscaped = false;
                                        }
                                        else if (searchingNextChar == char_BackSlash)
                                        {
                                            searchingEscaped = true;
                                        }
                                        else if (searchingNextChar == char_LeftParenthesis)
                                        {
                                            searchingGroups++;

                                            //Add empty group.
                                            groupList.Add(new Group() { success = false });
                                        }
                                        else if (searchingNextChar == char_RightParenthesis)
                                        {
                                            searchingGroups--;

                                            if (searchingGroups < 0)
                                                break;
                                        }

                                        patternIndex++;
                                    }

                                    patternIndex--;
                                }
                                else
                                {
                                    //Start alternative search.
                                    group.success = true;

                                    searchIndex = group.index;
                                    searchingChar = (char)searchList[searchIndex];
                                }
                                break;
                            case char_LeftParenthesis:
                                //Push the parent group on the stack.
                                groupStack.Push(group);

                                //Initializze the child group.
                                group = new Group() { index = searchIndex, patternBeginIndex = patternIndex, success = true };
                                groupList.Add(group);
                                break;
                            case char_RightParenthesis:
                                //Check if repetitions are evalueted.
                                if (group.repetitions == 0)
                                {
                                    //Evaluate repetitions.
                                    if (patternIndex + 1 < patternLength)
                                    {
                                        searchingNextChar = this.patternChars[patternIndex + 1];

                                        switch (searchingNextChar)
                                        {
                                            case char_Asterisk:
                                                patternIndex++;

                                                group.minRepetitions = 0;
                                                group.maxRepetitions = Int32.MaxValue;
                                                break;
                                            case char_PlusSign:
                                                patternIndex++;

                                                group.minRepetitions = 1;
                                                group.maxRepetitions = Int32.MaxValue;

                                                group.patternEndIndex = patternIndex + 1;
                                                break;
                                            case char_LeftCurlyBracket:
                                                group.minRepetitions = 0;
                                                group.maxRepetitions = 0;

                                                searchingCondition = false;

                                                patternIndex += 2;

                                                while (patternIndex < patternLength)
                                                {
                                                    searchingNextChar = this.patternChars[patternIndex];

                                                    if (searchingNextChar >= char_0 && searchingNextChar <= char_9)
                                                    {
                                                        if (searchingCondition)
                                                        {
                                                            group.maxRepetitions = maxRepetitions * 10 + (searchingNextChar - 0x30);
                                                        }
                                                        else
                                                        {
                                                            group.minRepetitions = minRepetitions * 10 + (searchingNextChar - 0x30);
                                                        }
                                                    }
                                                    else if (searchingNextChar == char_Comma)
                                                    {
                                                        searchingCondition = true;
                                                    }
                                                    else if (searchingNextChar == char_RightCurlyBracket)
                                                    {
                                                        break;
                                                    }

                                                    patternIndex++;
                                                }


                                                if (group.maxRepetitions == 0)
                                                    group.maxRepetitions = Int32.MaxValue;
                                                break;
                                        }
                                    }

                                    group.patternEndIndex = patternIndex;
                                }


                                if (group.success)
                                {
                                    group.repetitions++;
                                    group.captureList.Add(new Capture() { index = group.index + group.length, length = searchIndex - group.index - group.length });
                                    group.length = searchIndex - group.index;

                                    //Check if repetitions are completed.
                                    if (group.repetitions < group.maxRepetitions)
                                    {
                                        patternIndex = group.patternBeginIndex;
                                    }
                                    else
                                    {
                                        patternIndex = group.patternEndIndex;

                                        group.captures = new Capture[group.captureList.Count];
                                        group.captureList.CopyTo(group.captures);
                                        group.captureList = null;

                                        //Pop parent group.
                                        group = (Group)groupStack.Pop();
                                    }
                                }
                                //Check if minRepetitions are completed.
                                else if (group.repetitions >= group.minRepetitions)
                                {
                                    patternIndex = group.patternEndIndex;

                                    group.success = true;
                                    group.captures = new Capture[group.captureList.Count];
                                    group.captureList.CopyTo(group.captures);
                                    group.captureList = null;

                                    //Pop parent group.
                                    group = (Group)groupStack.Pop();
                                }
                                else
                                {
                                    patternIndex = group.patternEndIndex;

                                    //Pop parent group.
                                    group = (Group)groupStack.Pop();
                                    group.success = false;
                                }
                                break;
                            case char_LeftSquareBracket:
                                searchingEscaped = false;
                                searchingCondition = false;
                                searchingPreviousChar = (char)0;

                                classChars.Clear();

                                patternIndex++;

                                //Check if the class is negated.
                                if (this.patternChars[patternIndex] == char_CircumflexAccent)
                                {
                                    patternSymbol = symbol_NegatedClass;

                                    patternIndex++;
                                }
                                else
                                {
                                    patternSymbol = symbol_Class;
                                }

                                //Read class chars.
                                while (patternIndex < patternLength)
                                {
                                    searchingNextChar = this.patternChars[patternIndex];

                                    if (searchingEscaped)
                                    {
                                        searchingEscaped = false;

                                        if (searchingNextChar == char_d)
                                        {
                                            for (char symbolRangeByte = char_0; symbolRangeByte <= char_9; symbolRangeByte++)
                                                classChars.Add((char)symbolRangeByte);
                                        }
                                        else if (searchingNextChar == char_w)
                                        {
                                            for (char symbolRangeByte = char_0; symbolRangeByte <= char_9; symbolRangeByte++)
                                                classChars.Add((char)symbolRangeByte);

                                            for (char symbolRangeByte = char_A; symbolRangeByte <= char_Z; symbolRangeByte++)
                                                classChars.Add((char)symbolRangeByte);

                                            for (char symbolRangeByte = char_a; symbolRangeByte <= char_z; symbolRangeByte++)
                                                classChars.Add((char)symbolRangeByte);
                                        }
                                        else if (searchingNextChar == char_s)
                                        {
                                            classChars.Add(char_Space);
                                        }
                                        else if (searchingNextChar == char_r)
                                        {
                                            classChars.Add(char_CarriageReturn);
                                        }
                                        else if (searchingNextChar == char_n)
                                        {
                                            classChars.Add(char_LineFeed);
                                        }
                                        else
                                        {
                                            classChars.Add(searchingNextChar);
                                        }
                                    }
                                    else if (searchingNextChar == char_BackSlash)
                                    {
                                        searchingEscaped = true;
                                    }
                                    else if (searchingNextChar == char_MinusSign)
                                    {
                                        searchingPreviousChar = (char)classChars[classChars.Count - 1];
                                        searchingCondition = true;
                                    }
                                    else if (searchingNextChar == char_RightSquareBracket)
                                    {
                                        comparing = true;

                                        break;
                                    }
                                    else if (searchingCondition)
                                    {
                                        for (byte symbolRangeByte = (byte)(searchingPreviousChar + 1); symbolRangeByte <= searchingNextChar; symbolRangeByte++)
                                            classChars.Add((char)symbolRangeByte);

                                        searchingCondition = false;
                                    }
                                    else
                                    {
                                        classChars.Add(searchingNextChar);
                                    }

                                    patternIndex++;
                                }
                                break;
                            default:
                                patternSymbol = symbol_Char;
                                comparing = true;
                                break;
                        }
                    }


                    if (comparing)
                    {
                        comparing = false;

                        repetitions = 0;
                        minRepetitions = 1;
                        maxRepetitions = 1;


                        //Evaluate repetitions.
                        if (patternIndex + 1 < patternLength)
                        {
                            searchingNextChar = this.patternChars[patternIndex + 1];

                            switch (searchingNextChar)
                            {
                                case char_LeftCurlyBracket:
                                    minRepetitions = 0;
                                    maxRepetitions = 0;

                                    searchingCondition = false;

                                    patternIndex += 2;

                                    while (patternIndex < patternLength)
                                    {
                                        searchingNextChar = this.patternChars[patternIndex];

                                        if (searchingNextChar >= char_0 && searchingNextChar <= char_9)
                                        {
                                            if (searchingCondition)
                                            {
                                                maxRepetitions = maxRepetitions * 10 + (searchingNextChar - 0x30);
                                            }
                                            else
                                            {
                                                minRepetitions = minRepetitions * 10 + (searchingNextChar - 0x30);
                                            }
                                        }
                                        else if (searchingNextChar == char_Comma)
                                        {
                                            searchingCondition = true;
                                        }
                                        else if (searchingNextChar == char_RightCurlyBracket)
                                        {
                                            break;
                                        }

                                        patternIndex++;
                                    }


                                    if (maxRepetitions == 0)
                                        maxRepetitions = Int32.MaxValue;
                                    break;
                                case char_Asterisk:
                                    patternIndex++;

                                    minRepetitions = 0;
                                    maxRepetitions = Int32.MaxValue;
                                    break;
                                case char_PlusSign:
                                    patternIndex++;

                                    minRepetitions = 1;
                                    maxRepetitions = Int32.MaxValue;
                                    break;
                            }
                        }


                        //Check if repetitions are completed.
                        while (repetitions < maxRepetitions)
                        {
                            //Check symbol index.
                            if (searchIndex >= searchLength)
                            {
                                group.success = false;
                            }
                            else
                            {
                                searchingChar = (char)searchList[searchIndex];

                                //Check symbol matching.
                                switch (patternSymbol)
                                {
                                    case symbol_Class:
                                        group.success = classChars.IndexOf(searchingChar) != -1;
                                        break;
                                    case symbol_NegatedClass:
                                        group.success = classChars.IndexOf(searchingChar) == -1;
                                        break;
                                    case symbol_Digit:
                                        group.success = searchingChar >= char_0 && searchingChar <= char_9;
                                        break;
                                    case symbol_WordCharacter:
                                        group.success = (searchingChar >= char_0 && searchingChar <= char_9) || (searchingChar >= char_a && searchingChar <= char_z) || (searchingChar >= char_a && searchingChar <= char_z);
                                        break;
                                    default:
                                        group.success = searchingChar == patternChar;
                                        break;
                                }
                            }

                            if (group.success)
                            {
                                repetitions++;

                                searchIndex++;
                            }
                            //Check if minRepetitions are completed.
                            else if (repetitions >= minRepetitions)
                            {
                                group.success = true;

                                break;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }


                    patternIndex++;

                    if (patternIndex >= patternLength)
                        break;

                    patternChar = this.patternChars[patternIndex];


                    if (!group.success)
                    {
                        if (groupStack.Count == 0 && startAnchored)
                            break;

                        //Search group alternative or end. 
                        searchingGroups = 0;
                        searchingEscaped = false;

                        while (patternIndex < patternLength)
                        {
                            searchingNextChar = this.patternChars[patternIndex];

                            if (searchingEscaped)
                            {
                                searchingEscaped = false;
                            }
                            else if (searchingNextChar == char_BackSlash)
                            {
                                searchingEscaped = true;
                            }
                            else if (searchingNextChar == char_LeftParenthesis)
                            {
                                searchingGroups++;
                            }
                            else if (searchingNextChar == char_RightParenthesis)
                            {
                                searchingGroups--;

                                if (searchingGroups < 0)
                                    break;
                            }
                            else if (searchingGroups == 0 && searchingNextChar == char_VerticalLine)
                            {
                                break;
                            }

                            patternIndex++;
                        }

                        if (patternIndex >= patternLength)
                            break;

                        patternChar = this.patternChars[patternIndex];
                    }
                }

                if (match.success && patternIndex >= patternLength)
                {
                    match.length = searchIndex - match.index;
                    match.captures = new Capture[] { new Capture() { index = match.index, length = match.length } };

                    match.groups = new Group[groupList.Count];
                    groupList.CopyTo(match.groups);

                    return match;
                }
                else if (match.index < searchLength && !startAnchored)
                {
                    escaped = false;
                    comparing = false;
                    startAnchored = false;

                    match.index++;
                    match.success = true;

                    group = match;
                    groupList.Clear();
                    groupList.Add(group);
                    groupStack.Clear();

                    searchIndex = match.index;

                    patternIndex = 0;

                    patternChar = this.patternChars[patternIndex];
                }
                else
                {
                    match.index = 0;

                    break;
                }
            }

            return match;
        }
    }
}
