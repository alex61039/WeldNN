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
using BusinessLayer.Models.WeldingMachine;
using BusinessLayer.Welding.Machine;
using BusinessLayer.Welding.Configuration;
using System.Data.Entity;


namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class MaintenanceController : ControllerBaseAuthenticated
    {
        private MachineStateService _machineStateService;

        public MaintenanceController(
            WeldingContext context,
            MachineStateService machineStateService,
            Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
            _machineStateService = machineStateService;
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<Maintenance>> List()
        {
            var query = _context.Maintenances.Where(m => m.Status == (int)GeneralStatus.Active);

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                query = query
                    .Where(m => m.WeldingMachine.OrganizationUnit.OrganizationID == _userAccountOrganizationID);
            }


            var list = query
                .OrderByDescending(m => m.DateCreated)
                .ToList();

            return new APIResponse2<ICollection<Maintenance>>(list);
        }


        [HttpGet]
        public APIResponse2<Maintenance> Get(int id)
        {
            var item = _context.Maintenances.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<Maintenance>(404, "Not found");

            return new APIResponse2<Maintenance>(item);
        }

        [HttpPost]
        public APIResponse2<Maintenance> Save([FromBody] Maintenance item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("Maintenance", UserPermissionAccess.Write))
                return new APIResponse2<Maintenance>(403, "No access");

            // Validate



            // Load or create new
            Maintenance _item;
            if (item.ID > 0)
            {
                _item = _context.Maintenances.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<Maintenance>(404, "Not found");
            }
            else
            {
                // Working time
                var machine = _context.WeldingMachines.Find(item.WeldingMachineID);
                long? TimeTotalSecs = machine.TimeTotalSecs;
                long? TimeAfterLastServiceSecs = machine.TimeAfterLastServiceSecs;


                _item = new Maintenance
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    WeldingMachineID = item.WeldingMachineID,
                    CreatedUserID = _userAccount.ID,
                    MaintenanceStatus = (int)MaintenanceStatus.InProcess,
                    TotalTimeSec = TimeTotalSecs,
                    TimeSinceLastServiceSec = TimeAfterLastServiceSecs,
                    ResponsibleUserID = item.ResponsibleUserID.GetValueOrDefault() > 0 ? item.ResponsibleUserID.Value : (int?)null
                };

                _context.Maintenances.Add(_item);
            }

            var statusBefore = _item.MaintenanceStatus;

            // Update properties
            _item.MaintenanceStatus = item.MaintenanceStatus;
            _item.Description = item.Description;

            // Photo
            if (item.Photo != _item.Photo)
            {
                // Delete previous
                if (_item.Photo.HasValue)
                {
                    documentsService.Delete(_item.Photo.Value);
                }

                _item.Photo = item.Photo;
            }

            // Finished?
            if (statusBefore != item.MaintenanceStatus)
            {
                if (item.MaintenanceStatus == (int)MaintenanceStatus.Completed)
                {
                    _item.DateFinished = DateTime.Now;

                    // Update machine info
                    var machine = _context.WeldingMachines.Find(item.WeldingMachineID);
                    machine.LastServiceOn = DateTime.Now;
                    machine.TimeAfterLastServiceSecs = 0;
                    machine.UserServiceNotifiedBeforeHours = (int?)null;
                }
            }

            // Save
            _context.SaveChanges();

            return new APIResponse2<Maintenance>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("Maintenance", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.Maintenances.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }


            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            return new APIResponse(null);
        }

        // ===============================================================================
        public class MachineMaintenanceInfo
        {
            public WeldingMachine WeldingMachine { get; set; }

            public StateSummary StateSummary { get; set; }
        }

        [HttpGet]
        public APIResponse2<ICollection<MachineMaintenanceInfo>> ListInfo(int? OrganizationUnitID)
        {
            var configLoader = new WeldingMachineTypeConfigurationLoader(_context);

            var statuses = new int[] { (int)GeneralStatus.Active };

            // Include Organization Unit
            var query = _context.WeldingMachines
                .Where(m => statuses.Contains(m.Status));

            if (OrganizationUnitID.HasValue && OrganizationUnitID.Value > 0)
                query = query.Where(m => m.OrganizationUnitID == OrganizationUnitID.Value);

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                query = query
                    .Where(m => m.OrganizationUnit.OrganizationID == _userAccountOrganizationID);
            }

            var machines = query
            .OrderBy(m => m.Name);

            var list = new List<MachineMaintenanceInfo>();
            foreach (var m in machines)
            {
                var stateSummary = _machineStateService.GetCurrentWeldingMachineState(m.ID);

                var info = new MachineMaintenanceInfo
                {
                    WeldingMachine = m,
                    StateSummary = stateSummary
                };

                list.Add(info);
            }

            return new APIResponse2<ICollection<MachineMaintenanceInfo>>(list);
        }

    }
}
