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
using BusinessLayer.Welding.Machine;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class WeldingMachinesController : ControllerBaseAuthenticated
    {
        private MachineStateService _machineStateService;

        public WeldingMachinesController(
            WeldingContext context,
            MachineStateService machineStateService,
            Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
            _machineStateService = machineStateService;
        }

        // ==============================================================================================
        public class WeldingMachineInfo
        {
            public WeldingMachine WeldingMachine { get; set; }
            public BusinessLayer.Models.WeldingMachine.StateSummary StateSummary { get; set; }
        }

        [HttpGet]
        public APIResponse2<ICollection<WeldingMachineInfo>> ListInfo(int? organizationUnitID)
        {
            //if (!HasAccess("WeldingMachines", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<WeldingMachine>>(403, "No access");

            var statuses = new int[] { (int)GeneralStatus.Active };

            var query = _context.WeldingMachines
                .Where(m => statuses.Contains(m.Status));

            if (organizationUnitID.HasValue && organizationUnitID.Value > 0)
                query = query.Where(m => m.OrganizationUnitID == organizationUnitID.Value);

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                query = query.Where(m => m.OrganizationUnit.OrganizationID == _userAccountOrganizationID);
            }

            var machines = query
                .OrderBy(m => m.Name);

            var list = new List<WeldingMachineInfo>();
            foreach(var m in machines)
            {
                var stateSummary = _machineStateService.GetCurrentWeldingMachineState(m.ID);

                var info = new WeldingMachineInfo {
                    WeldingMachine = m,
                    StateSummary = stateSummary
                };

                list.Add(info);
            }

            return new APIResponse2<ICollection<WeldingMachineInfo>>(list);
        }

        [HttpGet]
        public APIResponse2<ICollection<WeldingMachine>> List()
        {
            //if (!HasAccess("WeldingMachines", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<WeldingMachine>>(403, "No access");

            var statuses = new int[] { (int)GeneralStatus.Active };

            var query = _context.WeldingMachines
                .Where(m => statuses.Contains(m.Status));

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                query = query.Where(m => m.OrganizationUnit.OrganizationID == _userAccountOrganizationID);
            }


            var list = query
                .OrderBy(m => m.Name)
                .ToList();

            return new APIResponse2<ICollection<WeldingMachine>>(list);
        }

        [HttpGet]
        public APIResponse2<ICollection<WeldingMachine>> ListByOrganizationUnit(int organizationUnitID)
        {
            //if (!HasAccess("WeldingMachines", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<WeldingMachine>>(403, "No access");

            var statuses = new int[] { (int)GeneralStatus.Active };

            var list = _context.WeldingMachines.Where(m => statuses.Contains(m.Status) && m.OrganizationUnitID == organizationUnitID).ToList();

            return new APIResponse2<ICollection<WeldingMachine>>(list);
        }

        [HttpGet]
        public APIResponse2<WeldingMachine> Get(int id)
        {
            //if (!HasAccess("WeldingMachines", PermissionAccess.Read))
            //    return new APIResponse2<WeldingMachine>(403, "No access");

            var item = _context.WeldingMachines.Where(m => m.ID == id && m.Status != (int)GeneralStatus.Deleted).FirstOrDefault();
            if (item == null)
                return new APIResponse2<WeldingMachine>(404, "Not found");

            return new APIResponse2<WeldingMachine>(item);
        }

        [HttpPost]
        public APIResponse2<WeldingMachine> Save([FromBody] WeldingMachine item)
        {
            if (!HasAccess("WeldingMachines", UserPermissionAccess.Write))
                return new APIResponse2<WeldingMachine>(403, "No access");

            // Validate

            // Check name
            if (_context.WeldingMachines.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            {
                return new APIResponse2<WeldingMachine>(2101, "Name already exists");
            }


            // Check MAC
            var clearedMac = string.IsNullOrWhiteSpace(item.MAC) ? "" : item.MAC.Replace(":", "").Replace("-", "");
            if (!String.IsNullOrWhiteSpace(clearedMac) && _context.WeldingMachines.Any(m => m.Status == (int)GeneralStatus.Active && m.MAC == clearedMac && m.ID != item.ID))
            {
                return new APIResponse2<WeldingMachine>(2107, "MAC-address already exists");
            }


            // Load or create new
            WeldingMachine _item;
            if (item.ID > 0)
            {
                _item = _context.WeldingMachines.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<WeldingMachine>(404, "Not found");
            }
            else
            {
                _item = new WeldingMachine
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.WeldingMachines.Add(_item);
            }

            // Update properties
            _item.OrganizationUnitID = item.OrganizationUnitID;
            _item.WeldingMachineTypeID = item.WeldingMachineTypeID;
            _item.Name = item.Name;
            _item.MAC = clearedMac;
            _item.SerialNumber = item.SerialNumber;
            _item.YearManufactured = item.YearManufactured;
            _item.DateStartedUsing = item.DateStartedUsing;
            _item.InventoryNumber = item.InventoryNumber;
            _item.MaintenanceRegulation = item.MaintenanceRegulation;
            _item.MaintenanceInterval = item.MaintenanceInterval;
            _item.Modules = item.Modules;
            _item.Label = item.Label;
            _item.Description = item.Description;



            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<WeldingMachine>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("WeldingMachines", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.WeldingMachines.Find(ID);
            if (wmt == null) {
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

        [HttpGet]
        public APIResponse2<BusinessLayer.Models.WeldingMachine.PanelState> PanelState(
            [FromQuery] int? ID,
            [FromQuery] int? TypeID,
            [FromQuery] bool test,
            [FromServices] MachineStateService machineStateService
            )
        {
            // Load configuration
            var configurationLoader = new BusinessLayer.Welding.Configuration.WeldingMachineTypeConfigurationLoader(_context);

            var configuration = ID.HasValue
                ? configurationLoader.LoadByMachine(ID.Value)
                : configurationLoader.LoadByType(TypeID.Value);

            if (configuration == null)
            {
                return new APIResponse2<BusinessLayer.Models.WeldingMachine.PanelState>(404, "");
            }


            var panelStateBuilder = new BusinessLayer.Welding.Panel.PanelStateBuilder(configuration, _context);
            BusinessLayer.Models.WeldingMachine.PanelState panelState = null;

            if (test)
            {
                panelState = panelStateBuilder.Immitate();
            }
            else
            {
                if (ID.HasValue)
                {
                    // Fetch state
                    var state = machineStateService.GetCurrentWeldingMachineState(ID.Value);

                    if (state != null)
                    {
                        panelState = panelStateBuilder.Build(state);
                    }
                }
            }

            return new APIResponse2<BusinessLayer.Models.WeldingMachine.PanelState>(panelState);
        }
    }
}
