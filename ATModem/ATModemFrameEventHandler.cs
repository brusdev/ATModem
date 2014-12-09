using System;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif

namespace BrusDev.IO.Modems
{
    public delegate void ATModemFrameEventHandler(object sender, ATModemFrameEventArgs e);
}
