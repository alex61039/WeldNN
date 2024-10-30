using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Notifications
{
    public abstract class NotificationTypeBase
    {
        public abstract string Type { get; }


        public abstract string GenerateKey();


        public abstract string GenerateJSON();


        public abstract string BuildContent();
    }
}
