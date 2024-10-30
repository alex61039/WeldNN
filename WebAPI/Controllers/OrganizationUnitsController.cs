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
    public class OrganizationUnitsController : ControllerBaseAuthenticated
    {
        public OrganizationUnitsController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<OrganizationUnit>> List()
        {
            //if (!HasAccess("OrganizationUnits", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<OrganizationUnit>>(403, "No access");

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                return new APIResponse2<ICollection<OrganizationUnit>>(_context.OrganizationUnits.Where(m => m.Status == (int)GeneralStatus.Active && m.OrganizationID == _userAccountOrganizationID).ToList());
            }

            // Добавить название организации
            var list = _context.OrganizationUnits
                .Include(ou => ou.Organization)
                .Where(m => m.Status == (int)GeneralStatus.Active)
                .ToList();

            list.ForEach(ou => {
                ou.Name = $"{ou.Name} ({ou.Organization.Name})";
            });

            return new APIResponse2<ICollection<OrganizationUnit>>(list);
        }


        [HttpGet]
        public APIResponse2<OrganizationUnit> Get(int id)
        {
            //if (!HasAccess("OrganizationUnits", PermissionAccess.Read))
            //    return new APIResponse2<OrganizationUnit>(403, "No access");

            var item = _context.OrganizationUnits.Include(ou => ou.Organization).Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<OrganizationUnit>(404, "Not found");

            // Добавить название организации, если админ
            if (HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                item.Name = $"{item.Name} ({item.Organization.Name})";
            }

            return new APIResponse2<OrganizationUnit>(item);
        }

        [HttpPost]
        public APIResponse2<OrganizationUnit> Save([FromBody] OrganizationUnit item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("OrganizationUnits", UserPermissionAccess.Write))
                return new APIResponse2<OrganizationUnit>(403, "No access");

            // Validate

            // Check name (within same organization)
            if (_context.OrganizationUnits.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID && m.OrganizationID == item.OrganizationID))
            {
                return new APIResponse2<OrganizationUnit>(2101, "Name already exists");
            }


            // Load or create new
            OrganizationUnit _item;
            if (item.ID > 0)
            {
                _item = _context.OrganizationUnits.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<OrganizationUnit>(404, "Not found");
            }
            else
            {
                _item = new OrganizationUnit
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.OrganizationUnits.Add(_item);
            }

            // Update properties
            _item.Name = item.Name;
            _item.OrganizationID = item.OrganizationID;
            _item.Scope = item.Scope;
            _item.Description = item.Description;
            _item.HeadUserId = item.HeadUserId;
            _item.Email = item.Email;
            _item.Phones = item.Phones;
            _item.Address = item.Address;

            if (_item.HeadUserId == 0)
                _item.HeadUserId = null;

            // Logo
            if (item.PlanImage != _item.PlanImage)
            {
                // Delete previous
                if (_item.PlanImage.HasValue)
                {
                    documentsService.Delete(_item.PlanImage.Value);
                }

                _item.PlanImage = item.PlanImage;
            }


            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<OrganizationUnit>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("OrganizationUnits", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.OrganizationUnits.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Has linked objects?
            if (_context.NetworkDevices.Any(m => m.Status == (int)GeneralStatus.Active && m.OrganizationUnitID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }
            else if (_context.WeldingMachines.Any(m => m.Status == (int)GeneralStatus.Active && m.OrganizationUnitID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse(null);
        }

        public class PlanDevice
        {
            public int ID { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public string Type { get; set; }
        }
        public class PlanDevicesSaveRequest
        {
            public int OrganizationUnitID { get; set; }
            public List<PlanDevice> Devices { get; set; }
        }

        [HttpPost]
        public APIResponse SavePlanDevices([FromBody] PlanDevicesSaveRequest req)
        {
            if (!HasAccess("OrganizationUnits", UserPermissionAccess.Write) && !HasAccess("NetworkDevices", UserPermissionAccess.Write) && !HasAccess("WeldingMachines", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            if (req!= null && req.Devices != null && req.OrganizationUnitID > 0)
            {
                foreach (var device in req.Devices)
                {
                    switch (device.Type)
                    {
                        case "router":
                            {
                                var nd = _context.NetworkDevices.Find(device.ID);
                                if (nd != null && nd.OrganizationUnitID == req.OrganizationUnitID)
                                {
                                    nd.PlanPositionX = device.X;
                                    nd.PlanPositionY = device.Y;
                                }
                                break;
                            }
                        case "weldingmachine":
                            {
                                var wm = _context.WeldingMachines.Find(device.ID);
                                if (wm != null && wm.OrganizationUnitID == req.OrganizationUnitID)
                                {
                                    wm.PlanPositionX = device.X;
                                    wm.PlanPositionY = device.Y;
                                }
                                break;
                            }
                    }
                }

                _context.SaveChanges();
            }

            return new APIResponse(null);
        }
    }
}
