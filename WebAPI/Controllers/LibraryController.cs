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
    public class LibraryController : ControllerBaseAuthenticated
    {
        public LibraryController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<LibraryDocument>> List()
        {
            return new APIResponse2<ICollection<LibraryDocument>>(_context.LibraryDocuments.Where(m => m.Status == (int)GeneralStatus.Active).ToList());
        }


        [HttpGet]
        public APIResponse2<LibraryDocument> Get(int id)
        {
            //if (!HasAccess("Organizations", PermissionAccess.Read))
            //    return new APIResponse2<Organization>(403, "No access");

            var item = _context.LibraryDocuments.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<LibraryDocument>(404, "Not found");

            return new APIResponse2<LibraryDocument>(item);
        }

        [HttpPost]
        public APIResponse2<LibraryDocument> Save([FromBody] LibraryDocument item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("LibraryDocuments", UserPermissionAccess.Write))
                return new APIResponse2<LibraryDocument>(403, "No access");

            // Validate

            // Check name
            if (_context.LibraryDocuments.Any(m => m.Status == (int)GeneralStatus.Active 
                && m.OriginalFilename == item.OriginalFilename
                && m.GroupName == item.GroupName
                && m.ID != item.ID))
            {
                return new APIResponse2<LibraryDocument>(2101, "Name already exists");
            }


            // Load or create new
            LibraryDocument _item;
            if (item.ID > 0)
            {
                _item = _context.LibraryDocuments.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<LibraryDocument>(404, "Not found");
            }
            else
            {
                _item = new LibraryDocument
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    UserAccountID = _userAccount.ID
                };

                _context.LibraryDocuments.Add(_item);
            }

            // Update properties
            _item.GroupName = item.GroupName;
            _item.OriginalFilename = item.OriginalFilename;

            // File
            if (item.Document != _item.Document)
            {
                // Delete previous
                try
                {
                    documentsService.Delete(_item.Document);
                }
                catch { }

                _item.Document = item.Document;
            }


            // Save
            _context.SaveChanges();

            return new APIResponse2<LibraryDocument>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("LibraryDocuments", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.LibraryDocuments.Find(ID);
            if (wmt == null) {
                return new APIResponse(404, "Not found");
            }

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            return new APIResponse(null);
        }
    }
}
