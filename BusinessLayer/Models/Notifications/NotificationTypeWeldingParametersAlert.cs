using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Notifications
{
    public class NotificationTypeWeldingParametersAlert : NotificationTypeBase
    {
        public NotificationTypeWeldingParametersAlert(NotificationWeldingParametersAlertParameters parameters)
        {
            Parameters = parameters;
        }

        public class ParameterAlert
        {
            public string PropertyCode { get; set; }
            public string PropertyDescription { get; set; }
            public string Unit { get; set; }

            /// <summary>
            /// Actual value from welding machine
            /// </summary>
            public string ActualValue { get; set; }

            /// <summary>
            /// Value set by limits, maybe a range, e.g. '80-120'
            /// </summary>
            public string LimitValue { get; set; }
        }

        /// <summary>
        /// Main Parameters object
        /// </summary>
        public class NotificationWeldingParametersAlertParameters
        {
            public int WeldingMachineID { get; set; }

            public string WeldingMachineLabel { get; set; }

            public DateTime AlertDatetime { get; set; }

            public ICollection<ParameterAlert> ParameterAlerts { get; set; }
        }

        public NotificationWeldingParametersAlertParameters Parameters { get; set; }

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
                result += String.Format("Выход параметров за пределы: {0}\n", Parameters.WeldingMachineLabel);
                result += "\n";

                // список параметров:
                foreach (var p in Parameters.ParameterAlerts)
                {
                    var unitTranslated = "";
                    if (!String.IsNullOrEmpty(p.Unit))
                        unitTranslated = p.Unit;

                    var propertyDescription = !String.IsNullOrEmpty(p.PropertyDescription) ? p.PropertyDescription : p.PropertyCode;

                    // State.I: 3.0 А (пределы -3 - 15.0)
                    result += String.Format("{0}: {1} {2} (пределы: {3})", propertyDescription, p.ActualValue, unitTranslated, p.LimitValue);
                    result += '\n';
                }

            }

            return result;
        }

        public override string Type
        {
            get
            {
                return "NotificationWeldingParametersLimit";
            }
        }
    }
}
