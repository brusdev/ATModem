using System;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif

namespace BrusDev.IO.Modems
{
    public delegate void ATModemDataEventHandler(object sender, ATModemDataEventArgs e);
}
