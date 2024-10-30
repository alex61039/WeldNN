using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Domain
{
    public class MachineStatesRepository
    {
        /// <summary>
        /// Dictionary of States by MACs
        /// </summary>
        static ConcurrentDictionary<string, BusinessLayer.Models.WeldingMachine.StateSummary> dict = new ConcurrentDictionary<string, BusinessLayer.Models.WeldingMachine.StateSummary>();
        static ConcurrentDictionary<string, DateTime> dictTime = new ConcurrentDictionary<string, DateTime>();

        static TimeSpan StateExpirationPeriod = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Enqueue state by machine's MAC-address
        /// </summary>
        static public void Set(string mac, BusinessLayer.Models.WeldingMachine.StateSummary state)
        {
            dict[mac.ToLower()] = state;
            dictTime[mac.ToLower()] = DateTime.Now;
        }

        static public bool TryGet(string mac, out BusinessLayer.Models.WeldingMachine.StateSummary state)
        {
            state = null;

            // No packets by the MAC
            if (!dict.ContainsKey(mac.ToLower()))
                return false;

            // check expiration
            var time = dictTime[mac.ToLower()];
            if (time.Add(StateExpirationPeriod) < DateTime.Now)
            {
                return false;
            }

            // Return packet
            state = dict[mac.ToLower()];

            return true;
        }
    }
}
