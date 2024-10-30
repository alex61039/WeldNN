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
using BusinessLayer.Welding.Controls;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class WeldingLimitProgramsController : ControllerBaseAuthenticated
    {
        ProgramControlsService _programControlsService;

        public WeldingLimitProgramsController(WeldingContext context,
            ProgramControlsService programControlsService,
            Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
            _programControlsService = programControlsService;
        }

        // ==============================================================================================
        public class WeldingLimitProgramsListFilters
        {
            public int Default { get; set; }    // -1 all, 0 default, 1 programs
            public int? UserAccountID { get; set; }
            public int? UserRoleID { get; set; }
            public int? WeldingMachineID { get; set; }
            public int? WeldingMachineTypeID { get; set; }
            public bool PastOnly { get; set; }
        }

        public class WeldingLimitProgramInfo
        {
            public WeldingLimitProgram WeldingLimitProgram { get; set; }
            public string WeldingMachineName { get; set; }
            public string WeldingMachineLabel { get; set; }
            public string WeldingMachineMAC { get; set; }
            public string WeldingMachineTypeName { get; set; }
            public string WeldingMachineTypeImage { get; set; }
            public string UserAccountName { get; set; }
            public string UserRoleName { get; set; }
            public ICollection<WeldingLimitProgramSchedule> Schedules { get; set; }
        }

        [HttpPost]
        public APIResponse2<ICollection<WeldingLimitProgramInfo>> List([FromBody] WeldingLimitProgramsListFilters filters)
        {
            //if (!HasAccess("Organizations", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<Organization>>(403, "No access");

            // Check that all active machines have default Program
            if (filters != null && filters.Default != 1)
            {
                var machinesWithoutDefaultProgram = _context.WeldingMachines
                    .Include(m => m.WeldingLimitPrograms)
                    .Where(m => m.Status == (int)GeneralStatus.Active
                        && !m.WeldingLimitPrograms.Any(p => p.IsMachineDefault))
                        .ToList();

                if (machinesWithoutDefaultProgram != null && machinesWithoutDefaultProgram.Count() > 0)
                {
                    foreach (var m in machinesWithoutDefaultProgram)
                    {
                        // Create default program
                        var defaultProgram = new WeldingLimitProgram
                        {
                            Status = (int)GeneralStatus.Active,
                            DateCreated = DateTime.Now,
                            CreatedUserID = _userAccount.ID,
                            DateUpdated = DateTime.Now,
                            UpdatedUserID = _userAccount.ID,
                            IsMachineDefault = true,
                            WeldingMachineID = m.ID,
                            ByBarcode = false
                        };

                        _context.WeldingLimitPrograms.Add(defaultProgram);
                    }

                    _context.SaveChanges();
                }
            }


            var query = _context.WeldingLimitPrograms
                .Where(m => m.Status == (int)GeneralStatus.Active
                    && (
                        m.WeldingMachineTypeID == null
                        || (m.WeldingMachineTypeID != null && m.WeldingMachineType.Status == (int)GeneralStatus.Active)
                        )
                    && (
                        m.WeldingMachineID == null
                        || (m.WeldingMachineID != null && m.WeldingMachine.Status == (int)GeneralStatus.Active)
                        )
                        );

            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                query = query
                    .Where(p => p.WeldingMachine.OrganizationUnit.OrganizationID == _userAccountOrganizationID);
            }


            // Build query depending on filters
            if (filters != null)
            {
                if (filters.Default == 0)
                    query = query.Where(p => p.IsMachineDefault);
                else if (filters.Default == 1)
                    query = query.Where(p => !p.IsMachineDefault);

                // UserAccount
                if (filters.UserAccountID.HasValue && filters.UserAccountID.Value > 0)
                    query = query.Where(p => p.UserAccountID == filters.UserAccountID.Value);

                // UserRole
                if (filters.UserRoleID.HasValue && filters.UserRoleID.Value > 0)
                    query = query.Where(p => p.UserRoleID == filters.UserRoleID.Value);

                // WeldingMachine
                if (filters.WeldingMachineID.HasValue && filters.WeldingMachineID.Value > 0)
                    query = query.Where(p => p.WeldingMachineID == filters.WeldingMachineID.Value);

                // WeldingMachineType
                if (filters.WeldingMachineTypeID.HasValue && filters.WeldingMachineTypeID.Value > 0)
                    query = query.Where(p => p.WeldingMachineTypeID == filters.WeldingMachineTypeID.Value);

                // Past Only - below
            }


            // Include all relative tables
            query = query
                .Include(p => p.WeldingMachine)
                .Include(p => p.WeldingMachine.WeldingMachineType)
                .Include(p => p.WeldingMachineType)
                .Include(p => p.UserAccount)
                .Include(p => p.UserRole)
                .Include(p => p.WeldingLimitProgramSchedules)
                .OrderByDescending(m => m.IsMachineDefault)
                .ThenBy(m => m.Name)
                .ThenBy(m => m.WeldingMachine.Name);

            // Filter PastOnly (only for Programs)
            if (filters != null)
            {
                var d = DateTime.Now.Date;

                //if (filters.PastOnly)
                //    query = query.Where(p => p.IsMachineDefault || p.WeldingLimitProgramSchedules.Any(s => !s.IsByWeekday && (s.DateTo == null || s.DateTo < d)));
                //else
                //    query = query.Where(p => p.IsMachineDefault || p.WeldingLimitProgramSchedules.Any(s => s.IsByWeekday || (s.DateTo == null || s.DateTo >= d)));
            }

            // Build Infos list
            var list = new List<WeldingLimitProgramInfo>();
            foreach (var p in query)
            {
                var info = new WeldingLimitProgramInfo
                {
                    WeldingLimitProgram = p,
                    WeldingMachineName = p.WeldingMachine != null ? p.WeldingMachine.Name : null,
                    WeldingMachineLabel = p.WeldingMachine != null ? p.WeldingMachine.Label : null,
                    WeldingMachineMAC = p.WeldingMachine != null ? p.WeldingMachine.MAC : null,
                    WeldingMachineTypeName = p.WeldingMachineType != null ? p.WeldingMachineType.Name : null,
                    WeldingMachineTypeImage = p.WeldingMachineType != null && p.WeldingMachineType.Photo.HasValue
                        ? p.WeldingMachineType.Photo.Value.ToString()
                        : null,

                    UserAccountName = p.UserAccount != null ? p.UserAccount.Name : null,
                    UserRoleName = p.UserRole != null ? p.UserRole.Name : null,
                    Schedules = p.WeldingLimitProgramSchedules
                };

                // Image
                if (p.WeldingMachineType != null && p.WeldingMachineType.Photo.HasValue)
                    info.WeldingMachineTypeImage = p.WeldingMachineType.Photo.Value.ToString();
                else if (p.WeldingMachine != null && p.WeldingMachine.WeldingMachineType != null && p.WeldingMachine.WeldingMachineType.Photo.HasValue)
                    info.WeldingMachineTypeImage = p.WeldingMachine.WeldingMachineType.Photo.Value.ToString();

                list.Add(info);
            }


            return new APIResponse2<ICollection<WeldingLimitProgramInfo>>(list);
        }

        [HttpGet]
        public APIResponse2<WeldingLimitProgram> Current(int weldingMachineID)
        {
            var currentProgramID = _programControlsService.GetCurrentWeldingMachineProgramID(weldingMachineID);

            var program = _context.WeldingLimitPrograms.Find(currentProgramID);

            return new APIResponse2<WeldingLimitProgram>(program);
        }

        [HttpGet]
        public APIResponse2<WeldingLimitProgram> Get(int id)
        {
            //if (!HasAccess("Organizations", PermissionAccess.Read))
            //    return new APIResponse2<Organization>(403, "No access");

            var item = _context.WeldingLimitPrograms
                .Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active)
                .Include(m => m.WeldingLimitProgramSchedules)
                .FirstOrDefault();
            if (item == null)
                return new APIResponse2<WeldingLimitProgram>(404, "Not found");

            return new APIResponse2<WeldingLimitProgram>(item);
        }

        [HttpPost]
        public APIResponse2<WeldingLimitProgram> CopyFrom([FromQuery] int WeldingLimitProgramID,
            [FromServices] ProgramControlsService programControlsService)
        {
            var item = _context.WeldingLimitPrograms
                .Include(m => m.WeldingLimitProgramSchedules)
                .FirstOrDefault(p => p.ID == WeldingLimitProgramID);
            if (item == null)
                return new APIResponse2<WeldingLimitProgram>(404, "Not found");

            // Detach
            _context.Entry(item).State = EntityState.Detached;

            // Reset some props
            item.ID = 0;
            item.Name = "";
            item.IsMachineDefault = false;
            item.ProgramStatus = (int)GeneralStatus.Inactive;

            var new_item = Save(item).Data;

            // Copy program values
            var values = programControlsService.LoadWeldingMachineProgramValues(WeldingLimitProgramID);
            if (values != null)
            {
                programControlsService.SaveWeldingMachineProgramValues(
                    new_item.ID,
                    values,
                    _userAccount.ID,
                    new_item.WeldingMaterialID.GetValueOrDefault()
                    );

            }

            return new APIResponse2<WeldingLimitProgram>(new_item);
        }

        [HttpPost]
        public APIResponse2<WeldingLimitProgram> Save([FromBody] WeldingLimitProgram item)
        {
            if (!HasAccess("WeldingLimitPrograms", UserPermissionAccess.Write))
                return new APIResponse2<WeldingLimitProgram>(403, "No access");

            // Validate
            // Check name
            //if (_context.WeldingLimitPrograms.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            //{
            //    return new APIResponse2<WeldingLimitProgram>(2101, "Name already exists");
            //}


            // Load or create new
            WeldingLimitProgram _item;
            if (item.ID > 0)
            {
                _item = _context.WeldingLimitPrograms.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active)
                    .Include(m => m.WeldingLimitProgramSchedules)
                    .FirstOrDefault();

                if (_item == null)
                    return new APIResponse2<WeldingLimitProgram>(404, "Not found");
            }
            else
            {
                _item = new WeldingLimitProgram
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    CreatedUserID = _userAccount.ID,
                    IsMachineDefault = false,
                    ProgramStatus = (int)GeneralStatus.Active
                };

                _context.WeldingLimitPrograms.Add(_item);
            }

            // Update properties (don't update Default flag)
            _item.UpdatedUserID = _userAccount.ID;
            _item.DateUpdated = DateTime.Now;
            _item.Name = item.Name;
            _item.ProgramStatus = _item.IsMachineDefault ? (int)GeneralStatus.Active : item.ProgramStatus;   // Default must be always active

            // Type is required
            _item.WeldingMachineTypeID = item.WeldingMachineTypeID.GetValueOrDefault() > 0 ? item.WeldingMachineTypeID : (int?)null;

            // Barcode or QR?
            if (item.ByBarcode.HasValue && item.ByBarcode.Value)
            {
                // Check barcode
                if (item.ProgramStatus == (int)GeneralStatus.Active && !String.IsNullOrEmpty(item.Barcode))
                {
                    var barcode_exists = _context.WeldingLimitPrograms.Any(p => p.Status == (int)GeneralStatus.Active
                        && p.ProgramStatus == (int)GeneralStatus.Active
                        && p.ID != _item.ID
                        && p.ByBarcode == true
                        && p.Barcode == item.Barcode);

                    if (barcode_exists)
                    {
                        return new APIResponse2<WeldingLimitProgram>(2106, "Barcode already exists");
                    }
                }

                _item.ByBarcode = true;
                _item.Barcode = item.Barcode;
                _item.WeldingMachineID = (int?)null;
                _item.UserAccountID = (int?)null;
                _item.UserRoleID = (int?)null;
            }
            else
            {
                _item.ByBarcode = false;
                _item.Barcode = null;
                _item.WeldingMachineID = item.WeldingMachineID.GetValueOrDefault() > 0 ? item.WeldingMachineID : (int?)null;
                _item.UserAccountID = item.UserAccountID.GetValueOrDefault() > 0 ? item.UserAccountID : (int?)null;
                _item.UserRoleID = item.UserRoleID.GetValueOrDefault() > 0 ? item.UserRoleID : (int?)null;
            }

            // Schedules
            if (item.WeldingLimitProgramSchedules == null) item.WeldingLimitProgramSchedules = new List<WeldingLimitProgramSchedule>();
            if (_item.WeldingLimitProgramSchedules == null) _item.WeldingLimitProgramSchedules = new List<WeldingLimitProgramSchedule>();

            // Remove
            var schedules_to_remove = _item.WeldingLimitProgramSchedules.Where(s => s.ID > 0 && !item.WeldingLimitProgramSchedules.Any(ss => ss.ID == s.ID)).ToList();
            foreach (var s in schedules_to_remove)
            {
                _context.WeldingLimitProgramSchedules.Remove(s);
                // _item.WeldingLimitProgramSchedules.Remove(s);
            }

            // Update
            foreach (var _s in _item.WeldingLimitProgramSchedules.Where(ss => ss.ID > 0))
            {
                var s = item.WeldingLimitProgramSchedules.FirstOrDefault(ss => ss.ID == _s.ID);
                if (s != null)
                {
                    _s.IsByWeekday = s.IsByWeekday;
                    _s.Mon = s.Mon;
                    _s.Tue = s.Tue;
                    _s.Wed = s.Wed;
                    _s.Thu = s.Thu;
                    _s.Fri = s.Fri;
                    _s.Sat = s.Sat;
                    _s.Sun = s.Sun;
                    _s.DateFrom = s.DateFrom;
                    _s.DateTo = s.DateTo;
                    _s.TimeFrom = s.TimeFrom;
                    _s.TimeTo = s.TimeTo;
                }
            }

            // Add new
            var schedules_to_add = item.WeldingLimitProgramSchedules.Where(s => s.ID == 0).ToList();
            foreach (var s in schedules_to_add)
            {
                _item.WeldingLimitProgramSchedules.Add(s);
            }



            // Save
            _context.SaveChanges();

            return new APIResponse2<WeldingLimitProgram>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("WeldingLimitPrograms", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.WeldingLimitPrograms.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }


            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            return new APIResponse(null);
        }
    }
}
