using BusinessLayer.Models;
using DataLayer.Welding;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Services.QueueTasks
{
    public class QueueTasksService
    {
        WeldingContext _context;

        public QueueTasksService(WeldingContext context)
        {
            _context = context;
        }

        public QueueTask CreateTask(int UserAccountID, string TaskName, object Parameters) {
            return CreateTask(UserAccountID, TaskName, Parameters, null, 0);
        }

        public QueueTask CreateTask(int UserAccountID, string TaskName, object Parameters, DateTime? ScheduleOn) {
            return CreateTask(UserAccountID, TaskName, Parameters, ScheduleOn, 0);
        }

        public QueueTask CreateTask(int UserAccountID, string TaskName, object Parameters, DateTime? ScheduleOn, int Priority) {

            string json = Parameters == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(Parameters);


            var task = new QueueTask
            {
                DateCreated = DateTime.Now,
                Status = (int)QueueTaskStatus.New,
                UserAccountID = UserAccountID,
                ScheduledOn = ScheduleOn,
                TaskName = TaskName,
                TaskParametersJSON = json,
                Priority = Priority
            };

            _context.QueueTasks.Add(task);
            _context.SaveChanges();

            return task;
        }

    }
}
