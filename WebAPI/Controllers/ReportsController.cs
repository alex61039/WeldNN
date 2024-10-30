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
using Microsoft.Extensions.Options;
using BusinessLayer.Interfaces.Storage;
using BusinessLayer.Services.QueueTasks;
using BusinessLayer.Models;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class ReportsController : ControllerBaseAuthenticated
    {
        QueueTasksService _queueTasksService;
        IDocumentsService _documentsService;

        public ReportsController(
            IDocumentsService documentsService, 
            QueueTasksService queueTasksService, 
            WeldingContext context, 
            Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
            _documentsService = documentsService;
            _queueTasksService = queueTasksService;
        }

        // ==============================================================================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Report(string guid)
        {
            Guid GUID;
            if (!Guid.TryParse(guid, out GUID))
                return BadRequest();

            FileStream stream;
            var doc = _documentsService.TryReadDocument(GUID, out stream);
            if (doc == null)
                return NotFound();

            return File(stream, doc.ContentType);
        }

        [HttpPost]
        public APIResponse Create([FromBody] ReportRequest req)
        {
            _queueTasksService.CreateTask(_userAccount.ID, "CreateReport", req);

            return new APIResponse(null);
        }
    }
}
