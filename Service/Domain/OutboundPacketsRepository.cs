using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Domain
{
    public class OutboundPacketsRepository
    {
        /// <summary>
        /// Dictionary of Packets by MACs
        /// </summary>
        static ConcurrentDictionary<string, Models.Packet> dict = new ConcurrentDictionary<string, Models.Packet>();

        /// <summary>
        /// Enqueue packet by machine's MAC-address
        /// </summary>
        /// <param name="mac"></param>
        /// <param name="packet"></param>
        static public void Set(string mac, Models.Packet packet)
        {
            dict[mac.ToLower()] = packet;
        }

        static public bool TryGet(string mac, out Models.Packet packet)
        {
            packet = null;

            // No packets by the MAC
            if (!dict.ContainsKey(mac.ToLower()))
                return false;

            // Return packet
            packet = dict[mac.ToLower()];

            return true;
        }
    }
}
