using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Notifications
{
    public class NotificationTypeMaintenance : NotificationTypeBase
    {
        public NotificationTypeMaintenance(
            int MachineID, 
            string MachineName, 
            string MAC, 
            int HoursBeforeService,
            int DaysSinceLastService)
        {
            Parameters = new NotificationMaintenanceParameters
            {
                WeldingMachineID = MachineID,
                WeldingMachineName = MachineName,
                WeldingMachineMAC = MAC,
                HoursBeforeService = HoursBeforeService,
                DaysSinceLastService = DaysSinceLastService
            };
        }

        /// <summary>
        /// Main Parameters object
        /// </summary>
        public class NotificationMaintenanceParameters
        {
            public int WeldingMachineID;
            public string WeldingMachineName;
            public string WeldingMachineMAC;

            /// <summary>
            /// Either DaysSinceLastService or HoursBeforeService
            /// </summary>
            public int HoursBeforeService;

            /// <summary>
            /// Either DaysSinceLastService or HoursBeforeService
            /// </summary>
            public int DaysSinceLastService;
        }

        public NotificationMaintenanceParameters Parameters { get; set; }

        public override string GenerateKey()
        {
            return Parameters == null ? null : String.Format("{0}_{1}", Parameters.WeldingMachineID, Parameters.HoursBeforeService);
        }

        public override string GenerateJSON()
        {
            if (Parameters == null)
                return "";

            return Newtonsoft.Json.JsonConvert.SerializeObject(Parameters);
        }

        public override string BuildContent()
        {
            string result = "";

            result += "Предстоит обслуживание аппарата:\n";
            result += "\n";

            if (Parameters != null)
            {
                if (Parameters.HoursBeforeService > 0)
                {
                    result += String.Format("{0} ({1}) - {2} часов до обслуживания",
                        Parameters.WeldingMachineName, Parameters.WeldingMachineMAC, Parameters.HoursBeforeService);
                }
                else if (Parameters.DaysSinceLastService > 0)
                {
                    result += String.Format("{0} ({1}) - {2} дней с последнего обслуживания",
                        Parameters.WeldingMachineName, Parameters.WeldingMachineMAC, Parameters.DaysSinceLastService);
                }
            }

            return result;
        }

        public override string Type
        {
            get
            {
                return "NotificationMaintenance";
            }
        }
    }
}
