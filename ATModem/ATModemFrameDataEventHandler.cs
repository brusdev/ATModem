using System;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif

namespace BrusDev.IO.Modems
{
    public delegate void ATModemFrameDataEventHandler(object sender, ATModemFrameDataEventArgs e);
}
