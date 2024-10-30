using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    public class WebAPIStep : IInstallationStep
    {
        public string GetName()
        {
            return "Web API";
        }

        public bool Process()
        {
            Console.WriteLine("Установка Web API");

            // Copy files
            copyFiles();

            // Process config
            processConfig();

            Console.WriteLine();
            return true;
        }

        void copyFiles()
        {
            // Web API
            var sourceDir = Path.Combine(Context.Instance.InstallerPath, @"dist/api");
            var destDir = Path.Combine(Context.Instance.InstallPath, @"api");

            Helper.xcopy(sourceDir, destDir);

            // Storage
            sourceDir = Path.Combine(Context.Instance.InstallerPath, @"dist/storage");
            destDir = Path.Combine(Context.Instance.InstallPath, @"storage");

            Helper.xcopy(sourceDir, destDir);
        }

        void processConfig()
        {
            var config_template = Path.Combine(Context.Instance.InstallPath, @"api/appsettings.json.template");
            var config_dest = Path.Combine(Context.Instance.InstallPath, @"api/appsettings.json");


            // Read config
            var config = File.ReadAllText(config_template);

            // Replace entities
            config = config.Replace("[DBSERVER]", Context.Instance.DBServer.Replace("\\", "\\\\"));
            config = config.Replace("[DBNAME]", Context.Instance.DBName.Replace("\\", "\\\\"));
            config = config.Replace("[DBUSER]", Context.Instance.DBUsername.Replace("\\", "\\\\"));
            config = config.Replace("[DBPASSWORD]", Context.Instance.DBPassword.Replace("\\", "\\\\"));
            config = config.Replace("[STORAGE_PATH]", Path.Combine(Context.Instance.InstallPath, @"storage\").Replace(@"\", @"\\"));

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
