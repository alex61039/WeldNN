using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Notifications
{
    public class NotificationTypeWeldingMachineAlert : NotificationTypeBase
    {
        public NotificationTypeWeldingMachineAlert(NotificationWeldingMachineAlertParameters parameters)
        {
            Parameters = parameters;
        }

        /// <summary>
        /// Main Parameters object
        /// </summary>
        public class NotificationWeldingMachineAlertParameters
        {
            public int WeldingMachineID { get; set; }

            public string WeldingMachineLabel { get; set; }

            public DateTime AlertDatetime { get; set; }

            public string Message { get; set; }
        }

        public NotificationWeldingMachineAlertParameters Parameters { get; set; }

        public override string GenerateKey()
        {
            // Build key by welding machine ID
            if (Parameters == null)
                return null;

            return Parameters.WeldingMachineID.ToString();
        }

        public override string GenerateJSON()
        {
            if (Parameters == null)
                return "";

            return Newtonsoft.Json.JsonConvert.SerializeObject(Parameters);
        }

        public override string BuildContent()
        {
            var result = "";

            if (Parameters != null)
            {
                // Выход параметров за пределы: <сварочный аппарат>
                result += String.Format("Уведомление от сварочного аппарата {0}\n", Parameters.WeldingMachineLabel);
                result += "\n";
                result += "\n";

                result += Parameters.Message;
            }

            return result;
        }

        public override string Type
        {
            get
            {
                return "NotificationWeldingMachineAlert";
            }
        }
    }
}
