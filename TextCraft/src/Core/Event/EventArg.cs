using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.Event
{
    public class EventArg
    {
    }

    public class LoadWorldEventArg:EventArg
    {
        public string name = "";
        public int seed;
    }

    public class UnLoadWorldEventArg : EventArg
    {
    }
}
