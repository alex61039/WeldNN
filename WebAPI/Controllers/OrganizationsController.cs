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
    public class OrganizationsController : ControllerBaseAuthenticated
    {
        public OrganizationsController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<Organization>> List()
        {
            //if (!HasAccess("Organizations", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<Organization>>(403, "No access");

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                return new APIResponse2<ICollection<Organization>>(_context.Organizations.Where(m => m.Status == (int)GeneralStatus.Active && m.ID == _userAccountOrganizationID).ToList());
            }


            return new APIResponse2<ICollection<Organization>>(_context.Organizations.Where(m => m.Status == (int)GeneralStatus.Active).ToList());
        }


        [HttpGet]
        public APIResponse2<Organization> Get(int id)
        {
            //if (!HasAccess("Organizations", PermissionAccess.Read))
            //    return new APIResponse2<Organization>(403, "No access");

            var item = _context.Organizations.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<Organization>(404, "Not found");

            return new APIResponse2<Organization>(item);
        }

        [HttpPost]
        public APIResponse2<Organization> Save([FromBody] Organization item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("Organizations", UserPermissionAccess.Write))
                return new APIResponse2<Organization>(403, "No access");

            // Validate

            // Check name
            if (_context.Organizations.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            {
                return new APIResponse2<Organization>(2101, "Name already exists");
            }


            // Load or create new
            Organization _item;
            if (item.ID > 0)
            {
                _item = _context.Organizations.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<Organization>(404, "Not found");
            }
            else
            {
                _item = new Organization
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.Organizations.Add(_item);
            }

            // Update properties
            _item.Name = item.Name;
            _item.FullName = item.FullName;
            _item.Scope = item.Scope;
            _item.Description = item.Description;
            _item.HeadUserId = item.HeadUserId;
            _item.Email = item.Email;
            _item.Website = item.Website;
            _item.Phones = item.Phones;
            _item.Address = item.Address;
            _item.INN = item.INN;
            _item.OGRN = item.OGRN;

            if (_item.HeadUserId == 0)
                _item.HeadUserId = null;

            // Logo
            if (item.Logo != _item.Logo)
            {
                // Delete previous
                if (_item.Logo.HasValue)
                {
                    documentsService.Delete(_item.Logo.Value);
                }

                _item.Logo = item.Logo;
            }


            // Save
            _context.SaveChanges();

            return new APIResponse2<Organization>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("Organizations", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.Organizations.Find(ID);
            if (wmt == null) {
                return new APIResponse(404, "Not found");
            }

            // Has linked Units/Divisions?
            if (_context.OrganizationUnits.Any(m=> m.Status == (int)GeneralStatus.Active && m.OrganizationID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            return new APIResponse(null);
        }
    }
}
