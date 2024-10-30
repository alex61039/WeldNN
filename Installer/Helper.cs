using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    public class Helper
    {
        static public string GenerateConnectionString(string dbserver, string dbname, string dbusername, string dbpassword)
        {
            return String.Format("Data Source={0};Initial Catalog={1};Integrated Security=False;User ID={2};Password={3};", dbserver, dbname, dbusername, dbpassword);
        }

        static public void execute(string filename, string arguments)
        {
            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;

            // Start the file
            startInfo.FileName = filename;

            //make the window Hidden
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            //Send the Source and destination as Arguments to the process
            startInfo.Arguments = arguments;

            try
            {
                // Start the process with the info we specified.
                // Call WaitForExit and then the using statement will close.
                using (Process exeProcess = Process.Start(startInfo))
                {
                    exeProcess.WaitForExit();
                }
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        static public void xcopy(string sourceDir, string destDir)
        {
            // /E - copies directories and subdirectories
            // /Y - answer Y
            // /I - assume destination is a folder
            // /Q - don't display filenames
            execute(
                "xcopy",
                "\"" + sourceDir + "\"" + " " + "\"" + destDir + "\"" + @" /E /Y /I /Q"
                );

        }
    }
}
