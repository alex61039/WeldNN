using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Notifications
{
    public class NotificationTypeMessage : NotificationTypeBase
    {
        public NotificationTypeMessage(string message)
        {
            Parameters = new NotificationMessageParameters
            {
                Message = message
            };
        }

        /// <summary>
        /// Main Parameters object
        /// </summary>
        public class NotificationMessageParameters
        {
            public string Message { get; set; }
        }

        public NotificationMessageParameters Parameters { get; set; }

        public override string GenerateKey()
        {
            return null;
        }

        public override string GenerateJSON()
        {
            if (Parameters == null)
                return "";

            return Newtonsoft.Json.JsonConvert.SerializeObject(Parameters);
        }

        public override string BuildContent()
        {
            return Parameters == null ? null : Parameters.Message;
        }

        public override string Type
        {
            get
            {
                return "NotificationMessage";
            }
        }
    }
}
