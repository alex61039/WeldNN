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
    public class WeldingAssemblyControlsController : ControllerBaseAuthenticated
    {
        public WeldingAssemblyControlsController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================

        public class WeldingAssemblyControlShort
        {
            public int ID { get; set; }
            public int WeldingDetailAssemblyTypeID { get; set; }

            public bool DeveloperSigned { get; set; }
            public string DeveloperName { get; set; }
            public string DeveloperSignedOn { get; set; }

            public bool ApproverSigned { get; set; }
            public string ApproverName { get; set; }
            public string ApproverSignedOn { get; set; }
        }

        [HttpGet]
        public APIResponse2<ICollection<WeldingAssemblyControlShort>> ListShort()
        {
            //if (!HasAccess("WeldingAssemblyControls", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<WeldingAssemblyControlShort>>(403, "No access");

            var list = _context.WeldingAssemblyControls
                .Where(m => m.Status == (int)GeneralStatus.Active)
                .ToList()
                .Select(i => new WeldingAssemblyControlShort
                {
                    ID = i.ID,
                    WeldingDetailAssemblyTypeID = i.WeldingDetailAssemblyTypeID,

                    DeveloperSigned = i.DeveloperSigned,
                    DeveloperName = i.DeveloperName,
                    DeveloperSignedOn = i.DeveloperSigned && i.DeveloperSignedOn.HasValue ? i.DeveloperSignedOn.Value.ToString("dd.MM.yyyy") : "",

                    ApproverSigned = i.ApproverSigned,
                    ApproverName = i.ApproverName,
                    ApproverSignedOn = i.ApproverSigned && i.ApproverSignedOn.HasValue ? i.ApproverSignedOn.Value.ToString("dd.MM.yyyy") : ""
                })
                .ToList();

            return new APIResponse2<ICollection<WeldingAssemblyControlShort>>(list);
        }


        [HttpGet]
        public APIResponse2<WeldingAssemblyControl> Get(int id)
        {
            //if (!HasAccess("WeldingAssemblyControls", PermissionAccess.Read))
            //    return new APIResponse2<WeldingAssemblyControl>(403, "No access");

            var item = _context.WeldingAssemblyControls.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<WeldingAssemblyControl>(404, "Not found");

            return new APIResponse2<WeldingAssemblyControl>(item);
        }

        [HttpPost]
        public APIResponse2<WeldingAssemblyControl> Save([FromBody] WeldingAssemblyControl item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("WeldingAssemblyControls", UserPermissionAccess.Write))
                return new APIResponse2<WeldingAssemblyControl>(403, "No access");

            // Validate

            // Check WeldNumber
            //if (_context.WeldingAssemblyControls.Any(m => m.Status == (int)GeneralStatus.Active 
            //    && m.WeldingDetailAssemblyTypeID == item.WeldingDetailAssemblyTypeID
            //    && m.ID != item.ID))
            //{
            //    return new APIResponse2<WeldingAssemblyInstruction>(2101, "Weld number already exists");
            //}


            // Load or create new
            WeldingAssemblyControl _item;
            if (item.ID > 0)
            {
                _item = _context.WeldingAssemblyControls.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<WeldingAssemblyControl>(404, "Not found");
            }
            else
            {
                _item = new WeldingAssemblyControl
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    CreatedUserID = _userAccount.ID
                };

                _context.WeldingAssemblyControls.Add(_item);
            }

            // Update properties
            _item.WeldingDetailAssemblyTypeID = item.WeldingDetailAssemblyTypeID;
            _item.JSON = item.JSON;
            _item.UpdatedDate = DateTime.Now;
            _item.UpdatedUserID = _userAccount.ID;

            // Developer Signed
            if (item.DeveloperSigned && !_item.DeveloperSigned)
            {
                _item.DeveloperSigned = true;
                _item.DeveloperUserID = _userAccount.ID;
                _item.DeveloperName = _userAccount.Name;
                _item.DeveloperSignedOn = DateTime.Now;
            }

            // Approver Signed
            if (item.ApproverSigned && !_item.ApproverSigned)
            {
                _item.ApproverSigned = true;
                _item.ApproverUserID = _userAccount.ID;
                _item.ApproverName = _userAccount.Name;
                _item.ApproverSignedOn = DateTime.Now;
            }


            // Save
            _context.SaveChanges();

            return new APIResponse2<WeldingAssemblyControl>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("WeldingAssemblyControls", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.WeldingAssemblyControls.Find(ID);
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
