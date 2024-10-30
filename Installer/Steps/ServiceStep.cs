using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    public class ServiceStep : IInstallationStep
    {
        public string GetName()
        {
            return "Сервис";
        }

        public bool Process()
        {
            Console.WriteLine("Установка windows-сервиса");

            // Install service
            installService();


            // Copy files
            copyFiles();

            // Process config
            processConfig();

            Console.WriteLine();
            return true;
        }

        void installService()
        {
            // Установить сервис
            var val = "";
            Console.Write("Установка сервиса, выберите режим (N - без установки, A - авто, M - ручной) [Nam]:");

            var keychar = Console.ReadKey().KeyChar;
            if (keychar == 'a' || keychar == 'A')
            {
                val = "A";
            }
            else if (keychar == 'm' || keychar == 'M')
            {
                val = "M";
            }
            else
            {
                val = "N";
            }



            try
            {
                if (val != "N")
                {
                    Helper.execute(
                        "sc",
                        String.Format(@"create ""WeldTelecom"" binPath=""{0}"" start={1}",
                            Path.Combine(Context.Instance.InstallPath, @"service\WeldingService.exe"),
                            val == "A" ? "auto" : "manual"
                            )
                        );
                }
            }
            catch { }

            Console.WriteLine("\n");
        }

        static public void StopService()
        {
            Console.WriteLine("Останавливаем сервис...");

            try
            {
                Helper.execute("net", "stop WeldTelecom");
            }
            catch { }
        }

        static public void StartService()
        {
            Console.WriteLine("Запускаем сервис...");

            try
            {
                Helper.execute("net", "start WeldTelecom");
            }
            catch { }
        }

        void copyFiles()
        {
            // Client
            var sourceDir = Path.Combine(Context.Instance.InstallerPath, @"dist/service");
            var destDir = Path.Combine(Context.Instance.InstallPath, @"service");

            Helper.xcopy(sourceDir, destDir);
        }

        void processConfig()
        {
            var config_template = Path.Combine(Context.Instance.InstallPath, @"service/WeldingService.exe.config.template");
            var config_dest = Path.Combine(Context.Instance.InstallPath, @"service/WeldingService.exe.config");


            // Read config
            var config = File.ReadAllText(config_template);

            // Replace entities
            config = config.Replace("[LOG_DIR]", Path.Combine(Context.Instance.InstallPath, @"service\_logs\"));
            config = config.Replace("[STORAGE_PATH]", Path.Combine(Context.Instance.InstallPath, @"storage\"));
            config = config.Replace("[DBSERVER]", Context.Instance.DBServer);
            config = config.Replace("[DBNAME]", Context.Instance.DBName);
            config = config.Replace("[DBUSER]", Context.Instance.DBUsername);
            config = config.Replace("[DBPASSWORD]", Context.Instance.DBPassword);
            config = config.Replace("[PORT]", Context.Instance.ServiceWeldingPort.ToString());

            config = config.Replace("[SMTP_Server]", Context.Instance.SMTP_Server);
            config = config.Replace("[SMTP_Login]", Context.Instance.SMTP_Login);
            config = config.Replace("[SMTP_Password]", Context.Instance.SMTP_Password);
            config = config.Replace("[SMTP_Port]", Context.Instance.SMTP_Port);
            config = config.Replace("[DefaultSenderName]", Context.Instance.DefaultSenderName);

            // Save to config
            try
            {
                File.Delete(config_dest);
            }
            catch { }

            File.WriteAllText(config_dest, config);
        }
    }
}
