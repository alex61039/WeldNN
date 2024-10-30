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
using Microsoft.Extensions.Configuration;
using BusinessLayer.Models;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Authorize]
    public class ControllerBaseAuthenticated : ControllerBase
    {
        private UserAccount __userAccount;
        private int? __userAccountOrganizationID;
        private int[] __UserAccountIrganizationUnitIDs;

        private IConfiguration _configuration;
        protected WeldingContext _context;
        protected AccountsManager accountsManager;

        public ControllerBaseAuthenticated(WeldingContext context, IConfiguration Configuration) : base()
        {
            _configuration = Configuration;

            // _context = context;

            // Create context every time
            _context = getContext();

            accountsManager = new AccountsManager(_context);

            // Retrieve user with roles
            initializeUser();
        }

        protected WeldingContext getContext()
        {
            return new DataLayer.Welding.WeldingContext(_configuration.GetConnectionString("DefaultConnection"), false);
        }

        /// <summary>
        /// Returns user's OrganizationID.
        /// Zero for superadmin.
        /// </summary>
        protected int _userAccountOrganizationID
        {
            get
            {
                if (__userAccount == null)
                {
                    initializeUser();
                }

                return __userAccountOrganizationID.GetValueOrDefault();
            }
        }

        protected int[] _userAccountOrganizationUnitIDs
        {
            get {
                if (__userAccount == null)
                {
                    initializeUser();
                }

                return __UserAccountIrganizationUnitIDs ?? new int[0];
            }
        }

        protected UserAccount _userAccount
        {
            get
            {
                if (__userAccount == null)
                {
                    initializeUser();
                }

                return __userAccount;
            }
        }

        private void initializeUser()
        {
            __userAccountOrganizationID = 0;

            if (User != null && User.Identity.IsAuthenticated)
            {
                __userAccount = accountsManager.GetUserAccountWithRoles(User.Identity.Name);

                // Update session
                if (__userAccount != null)
                {
                    try
                    {
                        string actionName = this.ControllerContext.RouteData.Values["action"].ToString().ToLower();
                        string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString().ToLower();

                        // Exclude some controllers/actions
                        if (!(controllerName == "notifications" && actionName == "shortinfo"))
                        {
                            _context.UpdateUserAccountSession(__userAccount.ID, null);
                        }
                    }
                    catch { }

                    // Retrive user's organization
                    if (__userAccount.OrganizationUnitID.HasValue)
                    {
                        // to avoid lazy loading for Organizations within UserAccounts
                        using (var __context = getContext())
                        {
                            __userAccountOrganizationID = __context.OrganizationUnits.Find(__userAccount.OrganizationUnitID.Value).OrganizationID;

                            __UserAccountIrganizationUnitIDs = __context.OrganizationUnits.Where(ou => ou.OrganizationID == __userAccountOrganizationID).Select(ou => ou.ID).ToArray();
                        }
                    }
                    else
                    {
                        __UserAccountIrganizationUnitIDs = new int[0];
                    }
                }
            }
        }

        protected bool HasAccess(string permissionName, UserPermissionAccess permissionAccess)
        {
            if (_userAccount == null)
            {
                initializeUser();
            }


            return accountsManager.HasAccess(_userAccount, permissionName, permissionAccess);
        }
    }
}
