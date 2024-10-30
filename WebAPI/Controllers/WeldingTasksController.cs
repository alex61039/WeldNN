using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DataLayer.Welding;
using BusinessLayer.Accounts;
using WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using WebAPI.Communication;
using BusinessLayer;
using System.IO;
using Microsoft.AspNetCore.Http;
using BusinessLayer.Configuration;
using BusinessLayer.Models;
using Microsoft.Extensions.Options;
using BusinessLayer.Interfaces.Storage;
using System.Data.Entity;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class WeldingTasksController : ControllerBaseAuthenticated
    {
        public WeldingTasksController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================

        public class WeldingTaskShort
        {
            public int ID { get; set; }
            public DateTime DateCreated { get; set; }
            public string DateCreatedText { get; set; }
            public string DateCreatedSortable { get; set; }


            public int DetailAssemblyTypeID { get; set; }
            public string DetailAssemblyTypeName { get; set; }
            public string DetailAssemblyTypeLabel { get; set; }
            public string DetailAssemblySerialNumber { get; set; }

            public string Title { get; set; }

            public int? WorkerUserAccountID { get; set; }
            public string WorkerName { get; set; }
            public int? ControllerUserAccountID { get; set; }
            public string ControllerName { get; set; }

            public int TaskStatus { get; set; }
        }

        [HttpGet]
        public APIResponse2<ICollection<WeldingTaskShort>> ListShort()
        {
            //if (!HasAccess("WeldingTasks", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<WeldingTaskShort>>(403, "No access");

            var list = _context.WeldingDetailAssemblyTasks
                .Where(t => t.Status == (int)GeneralStatus.Active)
                .Include(t => t.DetailAssembly)
                .Include(t => t.DetailAssembly.DetailAssemblyType)
                .OrderByDescending(t => t.DateModified)
                .Take(500)
                .Select(t => new WeldingTaskShort {
                    ID = t.ID,
                    DateCreated = t.DateCreated,

                    DetailAssemblyTypeID = t.DetailAssembly.DetailAssemblyTypeID,
                    DetailAssemblyTypeName = t.DetailAssembly.DetailAssemblyType.Name,
                    DetailAssemblyTypeLabel = t.DetailAssembly.DetailAssemblyType.Label,
                    DetailAssemblySerialNumber = t.DetailAssembly.SerialNumber,

                    Title = t.Title,

                    WorkerUserAccountID = t.WorkerUserID,
                    ControllerUserAccountID = t.ControllerUserID,

                    TaskStatus = t.TaskStatus.HasValue ? t.TaskStatus.Value : 0
                })
                .ToList();

            // Fill names
            foreach(var t in list)
            {
                // Dates
                t.DateCreatedText = t.DateCreated.ToString("dd.MM.yyyy");
                t.DateCreatedSortable = t.DateCreated.ToString("yyyy.MM.dd");

                // Worker
                if (t.WorkerUserAccountID.HasValue) {
                    var worker = _context.UserAccounts.FirstOrDefault(ua => ua.ID == t.WorkerUserAccountID.Value);
                    t.WorkerName = worker != null ? worker.Name : "";
                }

                // Controller
                if (t.ControllerUserAccountID.HasValue)
                {
                    var controller = _context.UserAccounts.FirstOrDefault(ua => ua.ID == t.ControllerUserAccountID.Value);
                    t.ControllerName = controller != null ? controller.Name : "";
                }
            }


            return new APIResponse2<ICollection<WeldingTaskShort>>(list);
        }


        public class WeldingTaskInfo
        {
            public WeldingDetailAssemblyTask Task { get; set; }
            public ICollection<WeldingDetailAssemblyTaskState> States { get; set; }

            public int DetailAssemblyTypeID { get; set; }
            public string DetailAssemblyTypeName { get; set; }
            public string DetailAssemblyTypeLabel { get; set; }
            public string DetailAssemblySerialNumber { get; set; }
            public string DetailAssemblyTypeImage { get; set; }

            public WeldingMachine WeldingMachine { get; set; }

            public UserAccount Worker { get; set; }
            public UserAccount Controller { get; set; }
        }

        [HttpGet]
        public APIResponse2<WeldingTaskInfo> Get(int id)
        {
            //if (!HasAccess("WeldingTasks", PermissionAccess.Read))
            //    return new APIResponse2<WeldingTaskInfo>(403, "No access");

            var task = _context.WeldingDetailAssemblyTasks.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active)
                .Include(t => t.DetailAssembly)
                .Include(t => t.DetailAssembly.DetailAssemblyType)
                .Include(t => t.WeldingDetailAssemblyTaskStates)
                .FirstOrDefault();
            if (task == null)
                return new APIResponse2<WeldingTaskInfo>(404, "Not found");

            var taskInfo = new WeldingTaskInfo {
                Task = task,
                States = task.WeldingDetailAssemblyTaskStates,

                DetailAssemblyTypeID = task.DetailAssembly.DetailAssemblyTypeID,
                DetailAssemblyTypeName = task.DetailAssembly.DetailAssemblyType.Name,
                DetailAssemblyTypeLabel = task.DetailAssembly.DetailAssemblyType.Label,
                DetailAssemblySerialNumber = task.DetailAssembly.SerialNumber,
                DetailAssemblyTypeImage = task.DetailAssembly.DetailAssemblyType.Image.HasValue ? task.DetailAssembly.DetailAssemblyType.Image.ToString() : "",

                WeldingMachine = task.WeldingMachineID.HasValue 
                    ? _context.WeldingMachines.FirstOrDefault(wm => wm.ID == task.WeldingMachineID.Value) : null,

                Worker = task.WorkerUserID.HasValue 
                    ? _context.UserAccounts.FirstOrDefault(ua => ua.ID == task.WorkerUserID.Value): null,
                Controller = task.ControllerUserID.HasValue
                    ? _context.UserAccounts.FirstOrDefault(ua => ua.ID == task.ControllerUserID.Value) : null,
            };

            // Clean user sensitive data
            if (taskInfo.Worker != null)
            {
                taskInfo.Worker.PasswordHash = null;
                taskInfo.Worker.PasswordSalt = null;
            }
            if (taskInfo.Controller != null)
            {
                taskInfo.Controller.PasswordHash = null;
                taskInfo.Controller.PasswordSalt = null;
            }

            return new APIResponse2<WeldingTaskInfo>(taskInfo);
        }

        [HttpPost]
        public APIResponse2<WeldingDetailAssemblyTask> Save([FromBody] WeldingDetailAssemblyTask item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("WeldingTasks", UserPermissionAccess.Write))
                return new APIResponse2<WeldingDetailAssemblyTask>(403, "No access");

            // Validate?

            WeldingDetailAssemblyTask _item;
            if (item.ID > 0)
            {
                _item = _context.WeldingDetailAssemblyTasks
                    .Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active)
                    .FirstOrDefault();

                if (_item == null)
                    return new APIResponse2<WeldingDetailAssemblyTask>(404, "Not found");

            }
            else
            {
                // Create new
                _item = new WeldingDetailAssemblyTask
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    CreatedUserID = _userAccount.ID,
                    TaskStatus = (int)WeldingTaskStatus.InProcess
                };

                _context.WeldingDetailAssemblyTasks.Add(_item);
            }

            // Update properties
            _item.DetailAssemblyID = item.DetailAssemblyID;
            _item.DateModified = DateTime.Now;
            _item.Title = item.Title;
            _item.Description = item.Description;
            _item.WorkerUserID = item.WorkerUserID.GetValueOrDefault() > 0 ? item.WorkerUserID : null;
            _item.ControllerUserID = item.ControllerUserID.GetValueOrDefault() > 0 ? item.ControllerUserID : null;
            _item.WeldingMachineID = item.WeldingMachineID.GetValueOrDefault() > 0 ? item.WeldingMachineID : null;
            _item.WeldingNumber = item.WeldingNumber;

            // Status updated (for already existing tasks)?
            if (item.ID > 0)
            {
                if (item.TaskStatus.HasValue && item.TaskStatus.Value > 0 && item.TaskStatus.Value != _item.TaskStatus.Value)
                {
                    // Save state
                    _context.WeldingDetailAssemblyTaskStates.Add(new WeldingDetailAssemblyTaskState {
                        TaskID = item.ID,
                        DateCreated = DateTime.Now,
                        CreatedUserID = _userAccount.ID,
                        CreatedUserName = _userAccount.Name,
                        TaskStatus = item.TaskStatus.Value
                    });

                    // Update status
                    _item.TaskStatus = item.TaskStatus;
                }
            }

            // Save
            _context.SaveChanges();

            return new APIResponse2<WeldingDetailAssemblyTask>(_item);
        }

        [HttpPost]
        public APIResponse2<WeldingDetailAssemblyTaskState> SaveState([FromBody] WeldingDetailAssemblyTaskState item)
        {
            //if (!HasAccess("WeldingTasks", PermissionAccess.Write))
            //    return new APIResponse2<WeldingDetailAssemblyTaskState>(403, "No access");

            // Validate?

            // Create new
            WeldingDetailAssemblyTaskState _item;

            _item = new WeldingDetailAssemblyTaskState
            {
                TaskID = item.TaskID,
                DateCreated = DateTime.Now,
                CreatedUserID = _userAccount.ID,
                CreatedUserName = _userAccount.Name,
                TaskStatus = item.TaskStatus,
                Comments = item.Comments
            };

            _context.WeldingDetailAssemblyTaskStates.Add(_item);

            // Update task's status
            var task = _context.WeldingDetailAssemblyTasks.Find(item.TaskID);
            task.TaskStatus = item.TaskStatus;

            // Save
            _context.SaveChanges();

            return new APIResponse2<WeldingDetailAssemblyTaskState>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("WeldingTasks", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.WeldingDetailAssemblyTasks.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Has linked Units/Divisions?
            //if (_context.OrganizationUnits.Any(m => m.Status == (int)GeneralStatus.Active && m.OrganizationID == ID))
            //{
            //    return new APIResponse(2100, "Cannot delete, objects attached.");
            //}

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            return new APIResponse(null);
        }
    }
}
