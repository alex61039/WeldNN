using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    public class WebClientStep : IInstallationStep
    {
        public string GetName()
        {
            return "Web Client";
        }

        public bool Process()
        {
            Console.WriteLine("Установка Web Client");

            // Copy files
            copyFiles();

            // Process config
            processConfig();

            Console.WriteLine();
            return true;
        }

        void copyFiles()
        {
            // Client
            var sourceDir = Path.Combine(Context.Instance.InstallerPath, @"dist/client");
            var destDir = Path.Combine(Context.Instance.InstallPath, @"client");

            Helper.xcopy(sourceDir, destDir);
        }

        void processConfig()
        {
            var config_template = Path.Combine(Context.Instance.InstallPath, @"client/config.json.template");
            var config_dest = Path.Combine(Context.Instance.InstallPath, @"client/config.json");


            // Read config
            var config = File.ReadAllText(config_template);

            // Replace entities
            config = config.Replace("[API_HOST]", Context.Instance.APIHost.Replace("\\", "\\\\"));

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
