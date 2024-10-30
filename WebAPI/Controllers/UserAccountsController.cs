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
    public class UserAccountsController : ControllerBaseAuthenticated
    {
        public UserAccountsController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<UserAccount>> List(bool? AllVisible)
        {
            //if (!HasAccess("UserAccounts", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<UserAccount>>(403, "No access");

            var statuses = new int[] { (int)GeneralStatus.Active };
            if (AllVisible.GetValueOrDefault())
                statuses = new int[] { (int)GeneralStatus.Active, (int)GeneralStatus.Inactive };

            var query = _context.UserAccounts
                .Where(m => statuses.Contains(m.Status) && !m.UserRole.IsAdmin)
                .Include(m => m.UserActs);


            // Only within certain Organization
            if (_userAccountOrganizationID > 0 && !HasAccess("ManageAllOrganizations", UserPermissionAccess.Read))
            {
                query = query.Where(m => m.OrganizationUnitID != null && m.OrganizationUnit.OrganizationID == _userAccountOrganizationID);
                // query = query.Where(m => m.OrganizationUnitID != null && _userAccountOrganizationUnitIDs.Contains(m.OrganizationUnitID.Value));
            }


            var list = query
                .OrderBy(m => m.Name)
                .ToList();

            // remove sensitive data
            list.ForEach(cleanUserAccountSensitiveData);

            return new APIResponse2<ICollection<UserAccount>>(list);
        }

        void cleanUserAccountSensitiveData(UserAccount ua)
        {
            ua.PasswordHash = null;
            ua.PasswordSalt = null;
        }

        [HttpGet]
        public APIResponse2<UserAccount> Get(int id)
        {
            //if (!HasAccess("UserAccounts", PermissionAccess.Read))
            //    return new APIResponse2<UserAccount>(403, "No access");

            var item = _context.UserAccounts
                .Where(m => m.ID == id && m.Status != (int)GeneralStatus.Deleted && !m.UserRole.IsAdmin)
                .Include(m => m.UserActs)
                .FirstOrDefault();

            if (item == null)
                return new APIResponse2<UserAccount>(404, "Not found");

            cleanUserAccountSensitiveData(item);

            return new APIResponse2<UserAccount>(item);
        }

        public class UserAccountSaveRequest
        {
            public UserAccount UserAccount { get; set; }
            public string Password { get; set; }
        }

        [HttpPost]
        public APIResponse2<UserAccount> Save([FromBody] UserAccountSaveRequest req, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("UserAccounts", UserPermissionAccess.Write))
                return new APIResponse2<UserAccount>(403, "No access");

            if (!accountsManager.CanManageUserRole(_userAccount, req.UserAccount.UserRoleID))
                return new APIResponse2<UserAccount>(403, "No access");

            // Validate
            var item = req.UserAccount;

            // Check username
            if (_context.UserAccounts.Any(m => m.Status == (int)GeneralStatus.Active && m.UserName == item.UserName && m.ID != item.ID))
            {
                return new APIResponse2<UserAccount>(2103, "Username already exists");
            }

            // Check name - maybe people with same name
            //if (_context.UserAccounts.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            //{
            //    return new APIResponse2<UserAccount>(2101, "Name already exists");
            //}

            // Check RFID
            if (!String.IsNullOrWhiteSpace(item.RFID) && _context.UserAccounts.Any(m => m.Status == (int)GeneralStatus.Active && m.RFID == item.RFID && m.ID != item.ID))
            {
                return new APIResponse2<UserAccount>(2102, "RFID already exists");
            }


            // Load or create new
            UserAccount _item;
            if (item.ID > 0)
            {
                _item = _context.UserAccounts
                    .Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active && !m.UserRole.IsAdmin)
                    .Include(m => m.UserActs)
                    .FirstOrDefault();

                if (_item == null)
                    return new APIResponse2<UserAccount>(404, "Not found");
            }
            else
            {
                _item = new UserAccount
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.UserAccounts.Add(_item);
            }

            // Update properties
            _item.OrganizationUnitID = item.OrganizationUnitID;
            _item.UserRoleID = item.UserRoleID;
            _item.Status = item.Status;
            _item.UserName = item.UserName;
            _item.Name = item.Name;
            _item.Email = item.Email;
            _item.Position = item.Position;
            _item.Category = item.Category;
            _item.PersonnelNumber = item.PersonnelNumber;
            _item.RecruitmentDate = item.RecruitmentDate;
            _item.BirthDate = item.BirthDate;
            _item.Education = item.Education;
            _item.Phone = item.Phone;
            _item.Address = item.Address;
            _item.Description = item.Description;
            _item.AllowEmailNotifications = item.AllowEmailNotifications;

            // Text '123,12345'
            _item.RFID = item.RFID;
            // Hex
            _item.RFID_Hex = BusinessLayer.Utils.RFIDHelper.Txt2Hex(item.RFID);

            // User acts
            var ids_arr = (item.UserActs != null) ? item.UserActs.Select(dpt => dpt.ID).ToArray() : new int[0];
            _item.UserActs = _context.UserActs.Where(ua => ids_arr.Contains(ua.ID)).ToList();

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

            // Save
            _context.SaveChanges();

            // Password?
            if (!String.IsNullOrWhiteSpace(req.Password))
            {
                var accountsManager = new AccountsManager(_context);
                accountsManager.ChangePassword(_item.ID, req.Password);
            }

            return new APIResponse2<UserAccount>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("UserAccounts", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.UserAccounts.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Load the UserRole (don't delete Admin)
            _context.Entry(wmt).Reference(p => p.UserRole).Load();
            if (wmt.UserRole.IsAdmin) {
                return new APIResponse(404, "Not found");
            }

            if (!accountsManager.CanManageUserRole(_userAccount, wmt.UserRoleID))
                return new APIResponse(403, "No access");


            // Has linked ...
            if (
                _context.Organizations.Any(m => m.Status == (int)GeneralStatus.Active && m.HeadUserId == ID)
                || _context.OrganizationUnits.Any(m => m.Status == (int)GeneralStatus.Active && m.HeadUserId == ID)
                )
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
