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
    public class WeldingControlResultsController : ControllerBaseAuthenticated
    {
        public WeldingControlResultsController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================

        public class WeldingControlResultShort
        {
            public int ID { get; set; }
            public int WeldingAssemblyControlID { get; set; }
            public int DetailAssemblyID { get; set; }
            public int DetailAssemblyTypeID { get; set; }

            public string DetailAssemblyTypeImage { get; set; }

            public string DetailAssemblyTypeName { get; set; }
            public string DetailAssemblyTypeLabel { get; set; }
            public string DetailAssemblySerialNumber { get; set; }

            public bool ControllerSigned { get; set; }
            public string ControllerName { get; set; }
            public string ControllerSignedOn { get; set; }

            public bool HeadSigned { get; set; }
            public string HeadName { get; set; }
            public string HeadSignedOn { get; set; }
        }

        [HttpGet]
        public APIResponse2<ICollection<WeldingControlResultShort>> ListShort()
        {
            //if (!HasAccess("WeldingControlResults", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<WeldingControlResultShort>>(403, "No access");

            var allAssemblyDetails = _context.DetailAssemblies
                .Where(d => d.Status == (int)GeneralStatus.Active)
                .Include(d => d.DetailAssemblyType)
                .Include(d => d.DetailAssemblyType.WeldingAssemblyControls)
                .ToList();

            var allControlResults = _context.WeldingAssemblyControlResults
                .Where(m => m.Status == (int)GeneralStatus.Active)
                .Include(m => m.WeldingAssemblyControl)
                .ToList();

            var list = new List<WeldingControlResultShort>();

            // Only ones have WeldingAssemblyControl
            foreach (var ad in allAssemblyDetails.Where(ad => ad.DetailAssemblyType.WeldingAssemblyControls.Any(wac => wac.Status == (int)GeneralStatus.Active)))
            {
                WeldingControlResultShort result;

                // Has a control?
                if (allControlResults.Any(cr => cr.DetailAssemblyID == ad.ID))
                {
                    list.AddRange(allControlResults
                        .Where(cr => cr.DetailAssemblyID == ad.ID)
                        .Select(cr => new WeldingControlResultShort
                        {
                            ID = cr.ID,
                            WeldingAssemblyControlID = cr.WeldingAssemblyControlID,
                            DetailAssemblyID = ad.ID,
                            DetailAssemblyTypeID = ad.DetailAssemblyTypeID,

                            DetailAssemblyTypeImage = ad.DetailAssemblyType.Image.ToString(),

                            DetailAssemblyTypeName = ad.DetailAssemblyType.Name,
                            DetailAssemblyTypeLabel = ad.DetailAssemblyType.Label,
                            DetailAssemblySerialNumber = ad.SerialNumber,

                            ControllerSigned = cr.ControllerSigned,
                            ControllerName = cr.ControllerName,
                            ControllerSignedOn = cr.ControllerSigned && cr.ControllerSignedOn.HasValue ? cr.ControllerSignedOn.Value.ToString("dd.MM.yyyy") : "",

                            HeadSigned = cr.HeadSigned,
                            HeadName = cr.HeadName,
                            HeadSignedOn = cr.HeadSigned && cr.HeadSignedOn.HasValue ? cr.HeadSignedOn.Value.ToString("dd.MM.yyyy") : ""
                        })
                        );
                }
                else
                {
                    // No control result yet
                    result = new WeldingControlResultShort {
                        ID = 0,
                        WeldingAssemblyControlID = ad.DetailAssemblyType.WeldingAssemblyControls.First(wac => wac.Status == (int)GeneralStatus.Active).ID,
                        DetailAssemblyID = ad.ID,
                        DetailAssemblyTypeID = ad.DetailAssemblyTypeID,

                        DetailAssemblyTypeImage = ad.DetailAssemblyType.Image.ToString(),

                        DetailAssemblyTypeName = ad.DetailAssemblyType.Name,
                        DetailAssemblyTypeLabel = ad.DetailAssemblyType.Label,
                        DetailAssemblySerialNumber = ad.SerialNumber,

                        ControllerSigned = false,
                        ControllerName = null,
                        ControllerSignedOn = "",

                        HeadSigned = false,
                        HeadName = null,
                        HeadSignedOn = ""
                    };

                    list.Add(result);
                }

            }


            return new APIResponse2<ICollection<WeldingControlResultShort>>(list);
        }


        [HttpGet]
        public APIResponse2<WeldingAssemblyControlResult> Get(int id)
        {
            //if (!HasAccess("WeldingControlResults", PermissionAccess.Read))
            //    return new APIResponse2<WeldingAssemblyControlResult>(403, "No access");

            var item = _context.WeldingAssemblyControlResults.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<WeldingAssemblyControlResult>(404, "Not found");

            return new APIResponse2<WeldingAssemblyControlResult>(item);
        }

        [HttpPost]
        public APIResponse2<WeldingAssemblyControlResult> Save([FromBody] WeldingAssemblyControlResult item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("WeldingControlResults", UserPermissionAccess.Write))
                return new APIResponse2<WeldingAssemblyControlResult>(403, "No access");

            // Validate

            // Check WeldNumber
            //if (_context.WeldingAssemblyControls.Any(m => m.Status == (int)GeneralStatus.Active 
            //    && m.WeldingDetailAssemblyTypeID == item.WeldingDetailAssemblyTypeID
            //    && m.ID != item.ID))
            //{
            //    return new APIResponse2<WeldingAssemblyInstruction>(2101, "Weld number already exists");
            //}


            // Load or create new
            WeldingAssemblyControlResult _item;
            if (item.ID > 0)
            {
                _item = _context.WeldingAssemblyControlResults.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<WeldingAssemblyControlResult>(404, "Not found");
            }
            else
            {
                _item = new WeldingAssemblyControlResult
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    CreatedUserID = _userAccount.ID
                };

                _context.WeldingAssemblyControlResults.Add(_item);
            }

            // Update properties
            _item.WeldingAssemblyControlID = item.WeldingAssemblyControlID;
            _item.DetailAssemblyID = item.DetailAssemblyID;
            _item.JSON = item.JSON;
            _item.UpdatedDate = DateTime.Now;
            _item.UpdatedUserID = _userAccount.ID;

            // Developer Signed
            if (item.ControllerSigned && !_item.ControllerSigned)
            {
                _item.ControllerSigned = true;
                _item.ControllerUserID = _userAccount.ID;
                _item.ControllerName = _userAccount.Name;
                _item.ControllerSignedOn = DateTime.Now;
            }

            // Approver Signed
            if (item.ControllerSigned && !_item.ControllerSigned)
            {
                _item.ControllerSigned = true;
                _item.ControllerUserID = _userAccount.ID;
                _item.ControllerName = _userAccount.Name;
                _item.ControllerSignedOn = DateTime.Now;
            }


            // Save
            _context.SaveChanges();

            return new APIResponse2<WeldingAssemblyControlResult>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("WeldingControlResults", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.WeldingAssemblyControlResults.Find(ID);
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
