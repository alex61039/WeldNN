using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Utils.Network
{
    public class NetworkHelper
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(uint destIP, uint srcIP, byte[] macAddress, ref uint macAddressLength);

        public static byte[] GetMacAddress(IPAddress address)
        {
            byte[] mac = new byte[6];

            try
            {
                uint len = (uint)mac.Length;
                byte[] addressBytes = address.GetAddressBytes();
                uint dest = ((uint)addressBytes[3] << 24)
                  + ((uint)addressBytes[2] << 16)
                  + ((uint)addressBytes[1] << 8)
                  + ((uint)addressBytes[0]);

                if (SendARP(dest, 0, mac, ref len) != 0)
                {
                    throw new Exception("The ARP request failed.");
                }
            }
            catch (Exception ex) {
                throw ex;
            }

            // return System.Text.Encoding.ASCII.GetString(mac, 0, mac.Length);
            return mac;
        }
    }
}
