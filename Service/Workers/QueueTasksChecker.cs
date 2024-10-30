using BusinessLayer.Models;
using BusinessLayer.Models.Notifications;
using BusinessLayer.Services.Notifications;
using BusinessLayer.Services.Reports;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Workers
{
    public class QueueTasksChecker : ServiceHelpers.PeriodicWorker
    {
        DateTime? lastCheckOn;

        public QueueTasksChecker(int periodSeconds) : base(periodSeconds)
        {
            Action = InternalCheck;
        }

        protected void InternalCheck()
        {
            List<QueueTask> tasks = null;

            using (var context = GetWeldingContext())
            {
                // Find any New task(s)
                tasks = context.QueueTasks
                        .Where(t => t.Status == (int)QueueTaskStatus.New
                            && (t.ScheduledOn == null || t.ScheduledOn.Value <= DateTime.Now))
                        .OrderByDescending(t => t.Priority)
                        .ThenBy(t => t.DateCreated)
                        .ToList();

                // Update them as processing
                if (tasks != null && tasks.Count > 0)
                {
                    foreach (var t in tasks)
                    {
                        t.Status = (int)QueueTaskStatus.InProcess;
                        t.DateStarted = DateTime.Now;
                    }

                    context.SaveChanges();
                }
            }

            // Process tasks
            if (tasks != null && tasks.Count > 0)
            {
                foreach (var task in tasks)
                {
                    ProcessTask(task);
                }
            }
        }

        protected void ProcessTask(QueueTask task)
        {
            // Prepare result
            var taskResult = new TaskResult
            {
                StatusResult = QueueTaskStatusResult.Ok,
                StatusMessage = null
            };

            // Do process
            try
            {
                switch (task.TaskName)
                {
                    case "CreateReport":
                        taskResult = Task_BuildReport(task);
                        break;
                }
            }
            catch (Exception ex)
            {
                taskResult.StatusResult = QueueTaskStatusResult.Error;
                taskResult.StatusMessage = String.Format("Exception: {0}", ex.ToString());
            }

            // Update result
            using (var context = GetWeldingContext())
            {
                var t = context.QueueTasks.Find(task.ID);

                t.Status = (int)QueueTaskStatus.Processed;
                t.DateFinished = DateTime.Now;
                t.StatusResult = (int)taskResult.StatusResult;
                t.StatusMessage = taskResult.StatusMessage;
                t.TaskResultJSON = taskResult.JSON;

                context.SaveChanges();
            }
        }

        public struct TaskResult
        {
            public QueueTaskStatusResult StatusResult;
            public string StatusMessage;
            public string JSON;
        }

        // ========================================================================================================================
        protected TaskResult Task_BuildReport(QueueTask task)
        {
            var result = new TaskResult {
                StatusMessage = null,
                StatusResult = QueueTaskStatusResult.Ok
            };


            using (var context = GetWeldingContext())            
            {
                var reportRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportRequest>(task.TaskParametersJSON);
                var reportBuilder = new ReportBuilder(
                    new Context.WeldingContextFactory(), 
                    GetBuildStorageOptions());

                var reportResult = reportBuilder.Build(task.UserAccountID.GetValueOrDefault(), reportRequest);

                // Create notification
                if (reportResult != null)
                {
                    var notification = new NotificationTypeReportCreated(task.ID, reportRequest.ReportType, reportResult.DocumentGUID);
                    var notificationService = new NotificationsService(context);

                    notificationService.Save(notification, task.UserAccountID.Value);
                }

                // Build task result
                result.StatusResult = (
                    reportResult != null
                    && (reportResult.DocumentGUID.HasValue || !String.IsNullOrEmpty(reportResult.JSON))
                    )
                    ? QueueTaskStatusResult.Ok : QueueTaskStatusResult.Error;

                result.StatusMessage = reportResult != null ? reportResult.ErrorMessage : null;
                result.JSON = reportResult?.JSON;
            }

            return result;
        }

        protected BusinessLayer.Configuration.StorageOptions GetBuildStorageOptions()
        {
            var opt = new BusinessLayer.Configuration.StorageOptions
            {
                StoragePath = ConfigurationManager.AppSettings["StoragePath"]
            };

            return opt;
        }
    }
}
