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

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class QueueTasksController : ControllerBaseAuthenticated
    {
        public QueueTasksController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        [HttpGet]
        public APIResponse2<QueueTask> Get(int id)
        {
            //if (!HasAccess("Organizations", PermissionAccess.Read))
            //    return new APIResponse2<Organization>(403, "No access");

            var item = _context.QueueTasks.Where(m => m.ID == id).FirstOrDefault();
            if (item == null)
                return new APIResponse2<QueueTask>(404, "Not found");

            return new APIResponse2<QueueTask>(item);
        }
    }
}
