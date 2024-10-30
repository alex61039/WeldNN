using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Domain
{
    public class IncomingPacketsQueue
    {
        static ConcurrentQueue<Models.Packet> _queue = new ConcurrentQueue<Models.Packet>();

        static public void Enqueue(Models.Packet packet)
        {
            if (_queue == null)
                _queue = new ConcurrentQueue<Models.Packet>();

            _queue.Enqueue(packet);
        }

        static public bool TryDequeue(out Models.Packet packet)
        {
            return _queue.TryDequeue(out packet);
        }

    }
}
