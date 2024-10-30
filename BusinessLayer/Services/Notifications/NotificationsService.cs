using BusinessLayer.Models;
using BusinessLayer.Models.Notifications;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Services.Notifications
{
    public class NotificationsService
    {
        WeldingContext _context;

        public NotificationsService(WeldingContext context)
        {
            _context = context;
        }

        public NotificationsShortInfo GetShortInfo(int UserAccountID)
        {
            var result = new NotificationsShortInfo();

            // Count unread
            result.CountUnread = _context.Notifications
                .Where(n => n.UserAccountID == UserAccountID && n.Status == (int)GeneralStatus.Active && !n.Read)
                .Count();

            return result;
        }

        public ICollection<Notification> ListUnread(int UserAccountID, int top = 10)
        {
            return _context.Notifications
                .Where(n => n.UserAccountID == UserAccountID && n.Status == (int)GeneralStatus.Active && !n.Read)
                .OrderByDescending(n => n.DateCreated)
                .Take(top)
                .ToList();
        }

        public ICollection<Notification> List(int UserAccountID, int top = 100)
        {
            return _context.Notifications
                .Where(n => n.UserAccountID == UserAccountID && n.Status == (int)GeneralStatus.Active)
                .OrderByDescending(n => n.DateCreated)
                .Take(top)
                .ToList();
        }


        public int SaveByUserPermission(NotificationTypeBase notificationBase, string permission, int? organizaionID)
        {
            // fetch users by role
            var accountsManager = new Accounts.AccountsManager(_context);
            var userAccounts = accountsManager.FindByPermission(permission, "READ", organizaionID);

            // save notification
            int result = 0;
            if (userAccounts != null)
            {
                foreach(var ua in userAccounts)
                {
                    Save(notificationBase, ua.ID);
                }

                result = userAccounts.Count();
            }

            return result;
        }

        public bool HasUnreadByKey(int userAccountID, string type, string key)
        {
            return _context.Notifications.Any(n => 
                n.UserAccountID == userAccountID 
                && n.Status == (int)GeneralStatus.Active 
                && !n.Read 
                && n.Type == type
                && n.Key == key);
        }

        public Notification Save(NotificationTypeBase notificationBase, int userAccountID)
        {
            // don't save is user already have UNREAD notification with same key
            var key = notificationBase.GenerateKey();
            if (!String.IsNullOrEmpty(key))
            {
                if (HasUnreadByKey(userAccountID, notificationBase.Type, key))
                    return null;
            }


            // Do save
            var notification = new Notification {
                DateCreated = DateTime.Now,
                Status = (int)GeneralStatus.Active,
                UserAccountID = userAccountID,
                Type = notificationBase.Type,
                Key = notificationBase.GenerateKey(),
                JSON = notificationBase.GenerateJSON(),
                Read = false
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();

            // Send Email
            try
            {
                var userAccount = _context.UserAccounts.Find(userAccountID);
                if (userAccount != null && !String.IsNullOrEmpty(userAccount.Email) && userAccount.AllowEmailNotifications.GetValueOrDefault())
                {
                    // Если уведомление пустое - не отправлять на почту
                    var notificationContent = notificationBase.BuildContent();

                    if (!String.IsNullOrEmpty(notificationContent))
                    {
                        var subject = "Новое уведомление от WeldTelecom";

                        var body = String.Format("Здравствуйте, {0}.\n", userAccount.Name);
                        body += "\n";
                        body += String.Format("У Вас новое уведомление на сайте WeldTelecom:");
                        body += "\n";
                        body += "\n";
                        body += notificationContent;

                        var mailer = new Mailer.MailerService(_context);
                        mailer.Create(userAccount.Email, userAccount.Name, subject, body);
                    }
                }
            }
            catch { }

            return notification;
        }

        public Notification Find(int ID)
        {
            var item = _context.Notifications.Find(ID);

            return item;
        }


        public void MarkRead(int ID)
        {
            var n = _context.Notifications.Find(ID);
            if (n == null)
                return;

            n.Read = true;
            _context.SaveChanges();
        }
    }
}
