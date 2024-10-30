using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Services.Mailer
{
    public class MailerService
    {
        WeldingContext _context;

        public MailerService(WeldingContext context)
        {
            _context = context;
        }

        public void Create(string ToEmail, string ToName, string Subject, string HtmlBody)
        {
            if (String.IsNullOrEmpty(ToEmail))
                return;


            var mail = new Mail
            {
                DateCreated = DateTime.Now,
                ToEmail = ToEmail,
                ToName = ToName,
                Subject = Subject,
                Body = HtmlBody
            };

            _context.Mails.Add(mail);
            _context.SaveChanges();
        }
    }
}
