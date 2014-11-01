using System;
using System.Text;

namespace BrusDev.Text.RegularExpressions
{
    // Summary:
    //     Represents the results from a single subexpression capture. BrusDev.Text.RegularExpressions.Capture
    //     represents one substring for a single successful capture.
    [Serializable]
    public class Capture
    {
        internal int index;
        internal int length;

        // Summary:
        //     The position in the original string where the first character of the captured
        //     substring was found.
        //
        // Returns:
        //     The zero-based starting position in the original string where the captured
        //     substring was found.
        public int Index { get { return this.index; } }
        //
        // Summary:
        //     The length of the captured substring.
        //
        // Returns:
        //     The length of the captured substring.
        public int Length { get { return this.length; } }
    }
}
