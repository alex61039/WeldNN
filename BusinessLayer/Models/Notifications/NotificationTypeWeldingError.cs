using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Notifications
{
    public class NotificationTypeWeldingError : NotificationTypeBase
    {
        public NotificationTypeWeldingError(NotificationWeldingErrorParameters parameters)
        {
            Parameters = parameters;
        }

        /// <summary>
        /// Main Parameters object
        /// </summary>
        public class NotificationWeldingErrorParameters
        {
            public int WeldingMachineID { get; set; }

            public string WeldingMachineName { get; set; }

            public string WeldingMachineLabel { get; set; }

            public string WeldingMachineMAC { get; set; }

            public DateTime AlertDatetime { get; set; }

            public string ErrorCode { get; set; }
        }

        public NotificationWeldingErrorParameters Parameters { get; set; }

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
                // Ошибка на аппарате: <сварочный аппарат>
                if (String.IsNullOrEmpty(Parameters.WeldingMachineLabel))
                    result += String.Format("Ошибка на аппарате: {0} ({1}):\n",
                        Parameters.WeldingMachineName,
                        Parameters.WeldingMachineMAC);
                else
                    result += String.Format("Ошибка на аппарате: {0} ({1}, {2}):\n",
                        Parameters.WeldingMachineName,
                        Parameters.WeldingMachineLabel,
                        Parameters.WeldingMachineMAC);

                result += "\n";

                // Код ошибки: ##
                result += String.Format("Код ошибки: {0}", Parameters.ErrorCode);
            }

            return result;
        }

        public override string Type
        {
            get
            {
                return "NotificationWeldingError";
            }
        }
    }
}
