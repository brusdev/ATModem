using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace System
{
    public static class Environment
    {
        public static int TickCount { get { return (int)(Utility.GetMachineTime().Ticks / 10000); } }
    }
}
