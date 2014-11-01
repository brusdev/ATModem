using System;
#if MF_FRAMEWORK
using Microsoft.SPOT;
#endif

namespace BrusDev.IO.Modems
{
    public delegate void ATModemEventHandler(object sender, EventArgs e);
}
