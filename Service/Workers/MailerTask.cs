using BusinessLayer.Models;
using BusinessLayer.Models.Notifications;
using BusinessLayer.Services.Notifications;
using BusinessLayer.Services.Reports;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Workers
{
    public class MailerTask : ServiceHelpers.PeriodicWorker
    {
        private const int CheckInterval = 35 * 1000; // milliseconds
        private bool busy = false;


        public MailerTask() : base(CheckInterval)
        {
            Action = InternalCheck;
        }

        protected void InternalCheck()
        {
            if (busy)
                return;

            busy = true;

            using (var context = GetWeldingContext())
            {
                MailerQueueProcess mailer = new MailerQueueProcess(context);
                mailer.Process();
            }

            busy = false;
        }
    }


    public class MailerQueueProcess
    {
        WeldingContext _context;

        SmtpClient client;

        string defaultSenderEmail;
        string defaultSenderName;
        int SMTPPort;
        string SMTPServer;
        string SMTPLogin;
        string SMTPPassword;


        public MailerQueueProcess(WeldingContext context)
        {
            _context = context;

            SMTPServer = ConfigurationManager.AppSettings["SMTP_Server"];
            SMTPLogin = ConfigurationManager.AppSettings["SMTP_Login"];
            SMTPPassword = ConfigurationManager.AppSettings["SMTP_Password"];

            try
            {
                SMTPPort = Convert.ToInt32(ConfigurationManager.AppSettings["SMTP_Port"]);
            }
            catch { }

            defaultSenderName = ConfigurationManager.AppSettings["DefaultSenderName"];
            defaultSenderEmail = SMTPLogin;

            if (!String.IsNullOrEmpty(SMTPServer))
            {
                client = new SmtpClient(SMTPServer, SMTPPort);
                if (!String.IsNullOrEmpty(SMTPLogin))
                {
                    client.Credentials = new NetworkCredential(SMTPLogin, SMTPPassword);
                }
                client.EnableSsl = true;
            }
            else
            {
                client = null;
            }

        }

        public int Process()
        {
            if (client == null)
                return 0;

            int total = 0;


            var mails = _context.Mails;

            foreach(var m in mails)
            {
                try
                {
                    if (isValidEmail(m.ToEmail))
                    {
                        SendMail(
                            m.ID,
                            m.ToEmail,
                            m.ToName,
                            defaultSenderEmail,
                            defaultSenderName,
                            m.Subject,
                            m.Body
                            );

                        total++;

                        Logger.Log(LogLevel.Notice, "MAILER: Sent mail to " + m.ToEmail);
                    }
                    else
                    {
                        Logger.Log(LogLevel.Notice, "MAILER: Invalid email: " + m.ToEmail);
                    }

                }
                catch (Exception ex) {
                    Logger.LogException(ex, "MAILER: Error sending mail to " + m.ToEmail + "\n" + ex.ToString());
                }

                try
                {
                    _context.Mails.Remove(m);
                }
                catch { }
            }

            _context.SaveChanges();


            return total;
        }

        void SendMail(int mail_id, string to_email, string to_name, string from_email, string from_name, string subject, string body)
        {
            AlternateView aview = null;
            string textBody = body;

            // HTML?
            bool isHtml = body.ToLower().IndexOf("<br") >= 0;


            if (isHtml)
            {
                textBody = System.Text.RegularExpressions.Regex.Replace(body, @"<(.|\n)*?>", string.Empty);

                Stream s = new MemoryStream(Encoding.UTF8.GetBytes(body));
                aview = new AlternateView(s, MediaTypeNames.Text.Html);
                aview.ContentType.CharSet = "UTF-8";
            }

            MailAddress mailTo = new MailAddress(to_email);
            try
            {
                mailTo = new MailAddress(to_email, to_name);
            }
            catch { }

            MailAddress mailFrom = new MailAddress(defaultSenderEmail);
            try
            {
                if (String.IsNullOrEmpty(from_name) && String.IsNullOrEmpty(from_email))
                {
                    mailFrom = new MailAddress(defaultSenderEmail, defaultSenderName);
                    // from = defaultSenderName + "<" + defaultSenderEmail + ">";
                }
                else
                {
                    mailFrom = new MailAddress(from_email, from_name);
                    // from = from_name + "<" + from_email + ">";
                }
            }
            catch { }

            using (MailMessage mailMessage = new MailMessage(mailFrom, mailTo)) // new MailMessage(from, to, subject, textBody);
            {
                mailMessage.Subject = subject;
                mailMessage.Body = textBody;
                mailMessage.IsBodyHtml = false;

                // mailMessage.BodyEncoding = Encoding.GetEncoding("windows-1252");
                mailMessage.BodyEncoding = Encoding.UTF8;

                if (isHtml)
                {
                    mailMessage.AlternateViews.Add(aview);
                }

                try
                {
                    client.Send(mailMessage);
                }
                catch (Exception ex) {
                    throw ex;
                }
            }

        }

        bool isValidEmail(string email)
        {
            if (String.IsNullOrWhiteSpace(email))
                return false;

            if (email.ToLower().IndexOf("@test.com") >= 0)
                return false;

            return true;
        }

    }

}
