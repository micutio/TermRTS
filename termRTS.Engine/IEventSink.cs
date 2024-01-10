using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace termRTS.Engine;

public interface IEventSink
{
    public void ProcessEvent(IEvent evt);
}

