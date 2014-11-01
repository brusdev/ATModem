using System;
using System.Text;

namespace BrusDev.Text.RegularExpressions
{
    // Summary:
    //     Group represents the results from a single capturing group. A capturing group
    //     can capture zero, one, or more strings in a single match because of quantifiers,
    //     so Group supplies a collection of BrusDev.Text.RegularExpressions.Capture
    //     objects.
    [Serializable]
    public class Group : Capture
    {
        internal Capture[] captures;
        internal bool success;

        internal int patternBeginIndex;
        internal int patternEndIndex;

        internal int repetitions = 0;
        internal int minRepetitions = 1;
        internal int maxRepetitions = 1;
        internal System.Collections.ArrayList captureList = new System.Collections.ArrayList();


        // Summary:
        //     Gets a collection of all the captures matched by the capturing group, in
        //     innermost-leftmost-first order (or innermost-rightmost-first order if the
        //     regular expression is modified with the BrusDev.Text.RegularExpressions.RegexOptions.RightToLeft
        //     option). The collection may have zero or more items.
        //
        // Returns:
        //     The collection of substrings matched by the group.
        public Capture[] Captures { get { return this.captures; } }
        //
        // Summary:
        //     Gets a value indicating whether the match is successful.
        //
        // Returns:
        //     true if the match is successful; otherwise, false.
        public bool Success { get { return this.success; } }
    }
}
