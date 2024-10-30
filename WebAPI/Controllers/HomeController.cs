using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public ActionResult<string> Index()
        {
            return "Welding Telecom API v1.0";
        }

        [HttpGet]
        public ActionResult<string> TestEmail([FromServices] BusinessLayer.Services.Mailer.MailerService mailer, string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                mailer.Create("sergi31117@gmail.com", "Sergi", "Test from Welding", "Hello world!");
            }
            else
            {
                mailer.Create(email, email, "Test mail from Welding", "Hello world!");
            }

            return "Email sent.";
        }
    }
}
