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

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class DocumentsController : ControllerBaseAuthenticated
    {
        IDocumentsService _documentsService;

        public DocumentsController(IDocumentsService documentsService, WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
            _documentsService = documentsService;
        }

        // ==============================================================================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Document(string guid)
        {
            Guid GUID;
            if (!Guid.TryParse(guid, out GUID))
                return BadRequest();

            FileStream stream;
            var doc = _documentsService.TryReadDocument(GUID, out stream);
            if (doc == null)
                return NotFound();

            return 
                String.IsNullOrEmpty(doc.Filename)
                ? File(stream, doc.ContentType)
                : File(stream, doc.ContentType, doc.Filename);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult DocumentInline(string guid)
        {
            Guid GUID;
            if (!Guid.TryParse(guid, out GUID))
                return BadRequest();

            FileStream stream;
            var doc = _documentsService.TryReadDocument(GUID, out stream);
            if (doc == null)
                return NotFound();

            // Inline (without filename)
            return File(stream, doc.ContentType);
        }

        [HttpGet]
        public APIResponse2<ICollection<Document>> Documents()
        {
            return new APIResponse2<ICollection<Document>>(_context.Documents.Take(50).ToList());
        }

        [HttpPost]
        public APIResponse2<Document> Upload(IFormFile file)
        {
            if (file.Length > 0)
            {

                var doc = _documentsService.AddDocument(file.OpenReadStream(), file.ContentType, file.FileName, _userAccount == null ? 0 : _userAccount.ID);

                return new APIResponse2<Document>(doc);
            }

            return new APIResponse2<Document>(400, "Bad request");
        }

        [HttpDelete]
        public APIResponse2<Document> Delete(Guid guid)
        {
            _documentsService.Delete(guid);

            return new APIResponse2<Document>(null);
        }
    }
}
