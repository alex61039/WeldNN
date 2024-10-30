using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Notifications
{
    public class NotificationTypeReportCreated : NotificationTypeBase
    {
        public NotificationTypeReportCreated(int queueTaskID, string reportType, Guid? documentGUID)
        {
            Parameters = new NotificationReportCreatedParameters
            {
                QueueTaskID = queueTaskID,
                ReportType = reportType,
                DocumentGUID = documentGUID.HasValue ? documentGUID.Value.ToString() : null
            };
        }

        /// <summary>
        /// Main Parameters object
        /// </summary>
        public class NotificationReportCreatedParameters
        {
            public int QueueTaskID { get; set; }

            public string ReportType { get; set; }

            public string DocumentGUID { get; set; }
        }

        public NotificationReportCreatedParameters Parameters { get; set; }

        public override string GenerateKey()
        {
            return Parameters == null ? null : String.Format("{0}_{1}", Parameters.ReportType, Parameters.QueueTaskID);
        }

        public override string GenerateJSON()
        {
            if (Parameters == null)
                return "";

            return Newtonsoft.Json.JsonConvert.SerializeObject(Parameters);
        }

        public override string BuildContent()
        {
            // не отправлять на почту это уведомление
            return null;
            
            /*
            var result = "";

            result += "Отчет доступен для скачивания.";

            return result;
            */
        }

        public override string Type
        {
            get
            {
                return "ReportCreated";
            }
        }
    }
}
