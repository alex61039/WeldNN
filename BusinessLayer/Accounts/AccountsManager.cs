using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BusinessLayer.Models;

namespace BusinessLayer.Accounts
{
    public class AccountsManager
    {
        WeldingContext _context;

        public AccountsManager(WeldingContext context)
        {
            _context = context;
        }

        public UserAccount Authenticate(string username, string password)
        {
            var ua = _context.AuthenticateUser(username, password).FirstOrDefault();

            if (ua == null)
            {
                // Increase Fails counter
                var ua1 = FindByUsername(username);
                if (ua1 != null)
                {
                    ua1.FailedLoginsCount = ua1.FailedLoginsCount.GetValueOrDefault() + 1;
                    _context.SaveChanges();
                }
            }

            return ua;
        }

        /// <summary>
        /// Active user by UserName
        /// </summary>
        /// <returns></returns>
        public UserAccount FindByUsername(string username)
        {
            return _context.UserAccounts
                .Where(u => u.UserName == username && u.Status == (int)GeneralStatus.Active)
                .Include(u => u.UserActs)
                .FirstOrDefault();
        }


        public UserAccount FindByRFID(string rfid_hex)
        {
            if (String.IsNullOrEmpty(rfid_hex))
                return null;

            // remove padding zeros: 00000ABCDE => ABCDE
            rfid_hex = rfid_hex.TrimStart('0');

            // add leading zeros to length 6: ABCDE => 0ABCDE
            rfid_hex = rfid_hex.PadLeft(6, '0');

            return _context.UserAccounts
                .Where(ua => ua.RFID_Hex == rfid_hex && ua.Status == (int)GeneralStatus.Active)
                .Include(ua => ua.UserActs)
                .FirstOrDefault();
        }

        public UserAccount GetUserAccountWithRoles(string username)
        {
            return  _context.UserAccounts
                // .Include("UserRole.UserRolePermissions")
                .Where(u => u.UserName == username && u.Status == (int)GeneralStatus.Active)
                .Include(u => u.UserActs)
                .Include("UserRole")
                .Include("UserRole.UserRolePermissions")
                .Include("UserRole.UserRolePermissions.UserPermission")
                .FirstOrDefault();
        }

        public UserAccount GetUserAccountWithRoles(int ID)
        {
            return _context.UserAccounts
                // .Include("UserRole.UserRolePermissions")
                .Where(u => u.ID == ID && u.Status == (int)GeneralStatus.Active)
                .Include(u => u.UserActs)
                .Include("UserRole")
                .Include("UserRole.UserRolePermissions")
                .Include("UserRole.UserRolePermissions.UserPermission")
                .FirstOrDefault();
        }

        /// <summary>
        /// </summary>
        /// <param name="permission"></param>
        /// <param name="permissionType">READ or WRITE</param>
        /// <returns></returns>
        public ICollection<UserAccount> FindByPermission(string permission, string permissionType, int? organizationID)
        {
            permissionType = permissionType == "READ" ? "READ" : "WRITE";
            bool? bRead = permissionType == "READ" ? true : (bool?)null;
            bool? bWrite = permissionType == "WRITE" ? true : (bool?)null;

            var query = _context.UserAccounts
                .Include(ua => ua.UserRole)
                .Include(ua => ua.UserRole.UserRolePermissions)
                .Where(u =>
                    u.Status == (int)GeneralStatus.Active
                    && u.UserRole != null
                    && u.UserRole.UserRolePermissions.Any(urp =>
                        ((permissionType == "READ" && urp.Read == true) || (permissionType == "WITE" && urp.Write == true))
                        && urp.UserPermission != null && urp.UserPermission.Name == permission)
                    );

            if (organizationID.HasValue)
                query = query.Where(u => u.OrganizationUnit.OrganizationID == organizationID);

            return query.ToList();
        }

        public ICollection<UserPermission> GetAllUserPermissions()
        {
            return _context.UserPermissions.ToList();
        }


        public void UpdateLastLogonDate(string username)
        {
            var userAccount = _context.UserAccounts.Where(u => u.UserName == username && u.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (userAccount != null)
            {
                userAccount.DateLastLogon = DateTime.Now;
                userAccount.FailedLoginsCount = 0;
                _context.SaveChanges();
            }
        }

        public void ChangePassword(int userAccountID, string password)
        {
            var userAccount = _context.UserAccounts.Find(userAccountID);
            if (userAccount == null) return;

            // generate salt
            var salt = generateRandonSalt(20);

            // Set password
            _context.ChangePassword(userAccountID, salt, password);

        }

        private string generateRandonSalt(int length)
        {
            Random random = new Random((int)DateTime.Now.Ticks);

            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }


        public bool HasAccess(UserAccount userAccount, string permissionName, UserPermissionAccess permissionAccess)
        {
            if (userAccount == null)
                return false;

            // No role?
            if (userAccount.UserRole == null)
                return false;

            // Admin has access to everything
            if (userAccount.UserRole.IsAdmin)
                return true;

            // No permissions at all?
            if (userAccount.UserRole.UserRolePermissions == null || userAccount.UserRole.UserRolePermissions.Count <= 0)
                return false;

            // Find the role
            var permission = userAccount.UserRole.UserRolePermissions.FirstOrDefault(urp => urp.UserPermission.Name == permissionName);

            // No such permission
            if (permission == null)
                return false;

            switch (permissionAccess)
            {
                case UserPermissionAccess.Read:
                    return permission.Read.GetValueOrDefault();

                case UserPermissionAccess.Write:
                    return permission.Write.GetValueOrDefault();


            }

            return false;
        }

        public bool CanManageUserRole(UserAccount userAccount, int UserRoleID)
        {
            if (userAccount?.UserRole == null)
                return false;

            if (userAccount.UserRole.IsAdmin)
                return true;

            int[] CanEditUserRolesArr = null;
            try
            {
                if (!String.IsNullOrEmpty(userAccount.UserRole.CanEditUserRolesJSON))
                    CanEditUserRolesArr = Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>(userAccount.UserRole.CanEditUserRolesJSON);
            }
            catch { }

            if (CanEditUserRolesArr != null && CanEditUserRolesArr.Contains(UserRoleID))
                return true;

            return false;
        }
    }
}
