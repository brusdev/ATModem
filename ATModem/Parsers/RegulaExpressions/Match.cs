using System;
using System.Text;

namespace BrusDev.Text.RegularExpressions
{
    // Summary:
    //     Represents the results from a single regular expression match.
    [Serializable]
    public class Match : Group
    {
        internal static Match empty = new Match();
        internal Group[] groups;

        // Summary:
        //     Gets the empty group. All failed matches return this empty match.
        //
        // Returns:
        //     An empty System.Text.RegularExpressions.Match.
        public static Match Empty { get { return Match.empty; } }
        //
        // Summary:
        //     Gets a collection of groups matched by the regular expression.
        //
        // Returns:
        //     The character groups matched by the pattern.
        public Group[] Groups { get { return this.groups; } }
    }
}
