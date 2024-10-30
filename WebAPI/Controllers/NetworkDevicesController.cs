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
    public class NetworkDevicesController : ControllerBaseAuthenticated
    {
        public NetworkDevicesController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<NetworkDevice>> List()
        {
            //if (!HasAccess("NetworkDevices", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<NetworkDevice>>(403, "No access");

            var statuses = new int[] { (int)GeneralStatus.Active };

            var query = _context.NetworkDevices.Where(m => statuses.Contains(m.Status));

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                query = query
                    .Where(d => d.OrganizationUnit.OrganizationID == _userAccountOrganizationID);
            }


            var list = query.ToList();

            return new APIResponse2<ICollection<NetworkDevice>>(list);
        }

        [HttpGet]
        public APIResponse2<ICollection<NetworkDevice>> ListByOrganizationUnit(int organizationUnitID)
        {
            //if (!HasAccess("NetworkDevices", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<NetworkDevice>>(403, "No access");

            var statuses = new int[] { (int)GeneralStatus.Active };

            var list = _context.NetworkDevices.Where(m => statuses.Contains(m.Status) && m.OrganizationUnitID == organizationUnitID).ToList();

            return new APIResponse2<ICollection<NetworkDevice>>(list);
        }

        [HttpGet]
        public APIResponse2<NetworkDevice> Get(int id)
        {
            //if (!HasAccess("NetworkDevices", PermissionAccess.Read))
            //    return new APIResponse2<NetworkDevice>(403, "No access");

            var item = _context.NetworkDevices.Where(m => m.ID == id && m.Status != (int)GeneralStatus.Deleted).FirstOrDefault();
            if (item == null)
                return new APIResponse2<NetworkDevice>(404, "Not found");

            return new APIResponse2<NetworkDevice>(item);
        }

        [HttpPost]
        public APIResponse2<NetworkDevice> Save([FromBody] NetworkDevice item)
        {
            if (!HasAccess("NetworkDevices", UserPermissionAccess.Write))
                return new APIResponse2<NetworkDevice>(403, "No access");

            // Validate

            // Check name
            if (_context.NetworkDevices.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            {
                return new APIResponse2<NetworkDevice>(2101, "Name already exists");
            }



            // Load or create new
            NetworkDevice _item;
            if (item.ID > 0)
            {
                _item = _context.NetworkDevices.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<NetworkDevice>(404, "Not found");
            }
            else
            {
                _item = new NetworkDevice
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.NetworkDevices.Add(_item);
            }

            // Update properties
            _item.OrganizationUnitID = item.OrganizationUnitID;
            _item.Name = item.Name;
            _item.Model = item.Model;
            _item.MAC = item.MAC.Replace(":", "").Replace("-", "");
            _item.IP = item.IP;
            _item.Port = item.Port;
            _item.InventoryNumber = item.InventoryNumber;
            _item.DeviceLogin = item.DeviceLogin;
            _item.DevicePassword = item.DevicePassword;
            _item.Description = item.Description;



            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<NetworkDevice>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("NetworkDevices", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.NetworkDevices.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Has linked ...
            //if (
            //    _context.Organizations.Any(m=> m.Status == (int)GeneralStatus.Active && m.HeadUserId == ID)
            //    || _context.OrganizationUnits.Any(m => m.Status == (int)GeneralStatus.Active && m.HeadUserId == ID)
            //    )
            //{
            //    return new APIResponse(2100, "Cannot delete, objects attached.");
            //}

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse(null);
        }
    }
}
