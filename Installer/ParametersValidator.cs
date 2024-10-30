using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SqlClient;

namespace Installer
{
    public class ParametersValidator
    {
        static public bool ValidPath(string path)
        {
            if (String.IsNullOrEmpty(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);

                if (String.IsNullOrEmpty(fullPath))
                    return false;

                if (!Path.IsPathRooted(path))
                    return false;

                DirectoryInfo dir = new DirectoryInfo(fullPath);
                if (!dir.Exists)
                    dir.Create();
            }
            catch
            {
                return false;
            }

            return true;
        }

        static public bool ValidDBConnection(string dbserver, string dbname, string dbusername, string dbpassword)
        {
            if (String.IsNullOrEmpty(dbserver) || String.IsNullOrEmpty(dbname) || String.IsNullOrEmpty(dbusername) || String.IsNullOrEmpty(dbpassword))
                return false;

            var connectionString = Helper.GenerateConnectionString(dbserver, dbname, dbusername, dbpassword);

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    var query = "select 1";
                    var command = new SqlCommand(query, connection);

                    connection.Open();
                    // Console.WriteLine("SQL Connection successful.");

                    command.ExecuteScalar();
                    // Console.WriteLine("SQL Query execution successful.");
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        static public bool ValidUrl(string url)
        {
            if (String.IsNullOrEmpty(url))
                return false;

            // Allow http://*:8000/
            url = url.Replace("*", "localhost");

            try
            {
                Uri uriResult;
                bool result = Uri.TryCreate(url, UriKind.Absolute, out uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                if (!result)
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        static public bool ValidPort(int port)
        {
            if (port <= 0)
                return false;

            return true;
        }
    }
}
