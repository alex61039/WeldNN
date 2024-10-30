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

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private AccountsManager _accountsManager;
        private readonly AuthenticationService _authenticationService;

        public AccountController(WeldingContext context, AuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
            _accountsManager = new AccountsManager(context);
        }

        /*
        [HttpGet]
        [Authorize]
        public ActionResult<APIResponse> TestAuth()
        {
            return new APIResponse(User.Identity.Name);
        }
        */


        public class CreateTokenRequest
        {
            [Required]
            public string username { get; set; }
            [Required]
            public string password { get; set; }
        }


        [HttpPost]
        public ActionResult<APIResponse> CreateToken([FromBody] CreateTokenRequest req)
        {
            if (!ModelState.IsValid)
            {
                return APIResponse.FromModelState(ModelState);
                // return BadRequest(ModelState);
            }

            var response = _authenticationService.CreateAccessToken(req.username, req.password);
            if (!response.Success)
            {
                // return BadRequest(response.Message);
                return new APIResponse(1000, response.Message);
            }

            // Update Last Logon date
            _accountsManager.UpdateLastLogonDate(req.username);


            return new APIResponse(response.Token);
        }

        [HttpPost]
        public ActionResult<Security.Tokens.AccessToken> RefreshToken(string refreshToken, string username)
        {
            var response = _authenticationService.RefreshToken(refreshToken, username);
            if (!response.Success)
            {
                return BadRequest(response.Message);
            }

            return response.Token;
        }

        [HttpPost]
        public IActionResult RevokeToken(string token)
        {
            _authenticationService.RevokeRefreshToken(token);

            return NoContent();
        }

        /// <summary>
        /// Returns UserAccount object
        /// </summary>
        [HttpGet]
        [Authorize]
        public ActionResult<APIResponse> CurrentUserAccount()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userAccount = _accountsManager.FindByUsername(User.Identity.Name);
                if (userAccount != null)
                {
                    // Remove some sensitive data
                    userAccount.PasswordHash = null;
                    userAccount.PasswordSalt = null;

                    return new APIResponse(userAccount);
                }
            }

            return new APIResponse(404, "User not found");
        }

        [HttpGet]
        [Authorize]
        public APIResponse2<UserRole> CurrentUserAccountRole()
        {
            if (!User.Identity.IsAuthenticated)
                return new APIResponse2<UserRole>(404, "User not found");

            var userAccount = _accountsManager.GetUserAccountWithRoles(User.Identity.Name);
            if (userAccount != null)
            {
                return new APIResponse2<UserRole>(userAccount.UserRole);
            }

            return new APIResponse2<UserRole>(404, "User not found");
        }

        [HttpGet]
        public APIResponse2<ICollection<UserPermission>> UserPermissions()
        {
            return new APIResponse2<ICollection<UserPermission>>(
                _accountsManager.GetAllUserPermissions()
                );
        }

    }
}
