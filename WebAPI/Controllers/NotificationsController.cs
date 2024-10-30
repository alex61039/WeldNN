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
using BusinessLayer.Services.Notifications;
using BusinessLayer.Models.Notifications;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class NotificationsController : ControllerBaseAuthenticated
    {
        NotificationsService _notificationsService;

        public NotificationsController(WeldingContext context, NotificationsService notificationsService, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
            _notificationsService = notificationsService;
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<NotificationsShortInfo> ShortInfo()
        {
            var result = _notificationsService.GetShortInfo(_userAccount.ID);

            return new APIResponse2<NotificationsShortInfo>(result);
        }

        [HttpGet]
        public APIResponse2<ICollection<Notification>> Unread()
        {
            var list = _notificationsService.ListUnread(_userAccount.ID);

            return new APIResponse2<ICollection<Notification>>(list);
        }

        [HttpGet]
        public APIResponse2<Notification> Get(int id)
        {
            var item = _notificationsService.Find(id);
            if (item == null || item.UserAccountID != _userAccount.ID)
                return new APIResponse2<Notification>(404, "Not found");

            return new APIResponse2<Notification>(item);
        }

        [HttpGet]
        public APIResponse2<ICollection<Notification>> List()
        {
            var list = _notificationsService.List(_userAccount.ID);

            return new APIResponse2<ICollection<Notification>>(list);
        }

        // ==============================================================================================
        [HttpDelete]
        public APIResponse MarkRead(int notificationID)
        {
            _notificationsService.MarkRead(notificationID);

            return new APIResponse(null);
        }
    }
}
