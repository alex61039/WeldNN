using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    public class Context
    {
        static public Context Instance = new Context();

        public bool IsNewInstallation {
            get
            {
                // return true;
                return Instance.InstalledBuildVersion <= 0;
            }
        }


        // From Installer
        public string InstallerPath { get; set; }

        public int CurrentBuildVersion { get; set; }



        // From Registry
        public string InstallPath { get; set; }

        public string DBServer { get; set; }
        public string DBUsername { get; set; }
        public string DBPassword { get; set; }
        public string DBName { get; set; }

        public string  APIHost { get; set; }

        public string SMTP_Server { get; set; }
        public string SMTP_Login { get; set; }
        public string SMTP_Password { get; set; }
        public string SMTP_Port { get; set; }
        public string DefaultSenderName { get; set; }

        // Порт для подключения сварочных аппаратов
        public int ServiceWeldingPort { get; set; }

        public int InstalledBuildVersion { get; set; }

        // Temporary
        public string AdminPassword { get; set; }
    }
}
