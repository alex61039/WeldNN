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
using BusinessLayer.Models;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class UsersController : ControllerBaseAuthenticated
    {
        public UsersController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        // ROLES
        [HttpGet]
        public APIResponse2<ICollection<UserRole>> Roles()
        {
            if (!HasAccess("UserRoles", UserPermissionAccess.Read) && !HasAccess("UserAccounts", UserPermissionAccess.Read))
                return new APIResponse2<ICollection<UserRole>>(403, "No access");

            // Do not show admin roles
            var roles = _context.UserRoles
                .Where(ur => !ur.IsAdmin && ur.Status == (int)GeneralStatus.Active)
                .OrderBy(ur => ur.Name)
                .ToList();

            var result = new APIResponse2<ICollection<UserRole>>(roles);

            return result;
        }

        [HttpDelete]
        public APIResponse DeleteRole(string roleName)
        {
            if (!HasAccess("UserRoles", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var role = _context.UserRoles.FirstOrDefault(ur => ur.Name == roleName);

            if (role != null)
            {
                // Check if there're users
                var usersCnt = _context.UserAccounts.Count(ua => ua.UserRoleID == role.ID && ua.Status == (int)GeneralStatus.Active);
                if (usersCnt > 0)
                {
                    return new APIResponse(2100, "Cannot delete, users attached.");
                }

                role.Status = (int)GeneralStatus.Deleted;
                _context.SaveChanges();
            }

            return new APIResponse(null);
        }

        [HttpGet]
        public APIResponse2<ICollection<UserRolePermission>> RolePermissions(string roleName)
        {
            if (!HasAccess("UserRoles", UserPermissionAccess.Read))
                return new APIResponse2<ICollection<UserRolePermission>>(403, "No access");

            var rolePermissions = _context.UserRolePermissions.Include("UserPermission")
                .Where(urp => urp.UserRole.Name == roleName)
                .ToList();

            return new APIResponse2<ICollection<UserRolePermission>>(rolePermissions);
        }

        public class SaveRolePermissionsRequest
        {
            public int ID { get; set; }
            public string RoleName { get; set; }
            public List<UserRolePermission> UserRolePermissions { get; set; }
            public ICollection<int> CanManageUserRoles { get; set; }
        }

        [HttpPost]
        public APIResponse SaveRolePermissions([FromBody] SaveRolePermissionsRequest req)
        {
            if (!HasAccess("UserRoles", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            UserRole role = null;

            // Check name exists
            if (_context.UserRoles.Any(ur => ur.Name == req.RoleName && ur.ID != req.ID && ur.Status == (int)GeneralStatus.Active))
            {
                return new APIResponse(2101, "Name already exists");
            }


            // New Role?
            if (req.ID == 0)
            {
                role = _context.UserRoles.Add(new UserRole
                {
                    Name = req.RoleName,
                    Status = (int)GeneralStatus.Active,
                    IsAdmin = false
                });

                _context.SaveChanges();
            }
            else
            {
                role = _context.UserRoles.Find(req.ID);

                // update properties
                role.Name = req.RoleName;

                _context.SaveChanges();
            }


            // Prepare array for CanManageRoles (ID = -1 means current role)
            int[] canManageRoles = req.CanManageUserRoles == null ? new int[0] : req.CanManageUserRoles.ToArray();

            if (canManageRoles.Contains(-1))
            {
                // Replace -1 by current role's ID
                canManageRoles[Array.IndexOf(canManageRoles, -1)] = role.ID;
            }

            role.CanEditUserRolesJSON = Newtonsoft.Json.JsonConvert.SerializeObject(canManageRoles);
            _context.SaveChanges();



            int userRoleId = role.ID;

            
            // Permissions
            // Delete all
            _context.UserRolePermissions.RemoveRange(
                _context.UserRolePermissions.Where(urp => 
                    urp.UserRole.Name == req.RoleName)
                );
            _context.SaveChanges();


            // Add
            _context.UserRolePermissions.AddRange(
                req.UserRolePermissions.Select(urp=> new UserRolePermission {
                    UserRoleID = userRoleId,
                    UserPermissionID = urp.UserPermissionID,
                    Read = urp.Read,
                    Write = urp.Write
                })
                );
            _context.SaveChanges();


            return new APIResponse(null);
        }

        // ==============================================================================================
        // PERMISSIONS
        [HttpGet]
        [AllowAnonymous]
        public APIResponse2<ICollection<UserPermission>> Permissions()
        {
            return new APIResponse2<ICollection<UserPermission>>(_context.UserPermissions.ToList());
        }

        // ==============================================================================================
        // USER ACTS (worker, controller, etc.)
        [HttpGet]
        public APIResponse2<ICollection<UserAct>> Acts()
        {
            return new APIResponse2<ICollection<UserAct>>(_context.UserActs.ToList());
        }

    }
}
