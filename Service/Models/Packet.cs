using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Models
{
    public class Packet
    {
        public string IP { get; set; }
        public string MAC { get; set; }
        public string Data { get; set; }
    }
}
