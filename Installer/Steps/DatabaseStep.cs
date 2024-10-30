using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    public class DatabaseStep : IInstallationStep
    {
        DataLayer dl;

        public DatabaseStep()
        {
            var connectionString = Helper.GenerateConnectionString(Context.Instance.DBServer, Context.Instance.DBName, Context.Instance.DBUsername, Context.Instance.DBPassword);
            dl = new DataLayer(connectionString);
        }

        public string GetName()
        {
            return "База данных";
        }

        /// <summary>
        /// dist/db/0...n folders
        /// </summary>
        /// <returns></returns>
        public bool Process()
        {
            Console.WriteLine("Выполнение скриптов базы данных");

            // Get current version
            int version = getCurrentVersion();

            // read all folders
            var db_path = Path.Combine(Context.Instance.InstallerPath, @"dist\db");
            var directories = Directory.GetDirectories(db_path);

            // sort by name-number
            var sorted_dirs = directories.OrderBy(d =>
            {
                var dir = Path.GetFileName(d);
                dir = dir.PadLeft(20, '0');
                return dir;
            });

            // Start transaction
            SqlTransaction transaction = null;
            // var transaction = dl.BeginTransaction("INSTALL");
            dl.ExecuteBatch_BeginTransaction();

            try
            {
                int max_version = version;
                foreach (var dir in sorted_dirs)
                {
                    if (Int32.TryParse(Path.GetFileName(dir), out int dir_numeric))
                    {
                        if (dir_numeric > version)
                        {
                            // process scripts within the folder
                            processDirectoryScripts(dir, transaction);

                            // processed
                            if (dir_numeric > max_version)
                                max_version = dir_numeric;
                        }
                    }
                }


                // Admin password?
                if (Context.Instance.IsNewInstallation && !String.IsNullOrEmpty(Context.Instance.AdminPassword)) {
                    // Add admin
                    var query = String.Format(@"IF NOT EXISTS(SELECT * FROM UserAccount WHERE UserName='admin')
                        INSERT INTO UserAccount (UserRoleID, Status, DateCreated, UserName, Name) VALUES (1, 1, GETDATE(), 'admin', 'Admin')");
                    dl.ExecuteBatchQuery(query);

                    // Set password
                    var salt = generateRandonSalt(20);
                    query = String.Format(@"UPDATE UserAccount SET 
                        PasswordSalt = N'{0}',
                        PasswordHash = HASHBYTES('SHA2_512', N'{1}')
                        WHERE UserName='admin'",
                        dl.Escape(salt),
                        dl.Escape(Context.Instance.AdminPassword + salt));
                    dl.ExecuteBatchQuery(query);
                }

                // commit
                // dl.CommitTransaction(transaction);
                dl.ExecuteBatch_CommitTransaction();

                if (max_version > version)
                    setCurrentVersion(max_version);
            }
            catch (Exception ex)
            {
                // Rollback
                try
                {
                    // dl.RollbackTransaction(transaction);
                    dl.ExecuteBatch_RollbackTransaction();
                }
                catch { }

                throw ex;
            }

            Console.WriteLine();
            return true;
        }

        void processDirectoryScripts(string dir, SqlTransaction transaction)
        {
            var directory = new DirectoryInfo(dir);
            var files = directory.GetFiles();

            foreach(var file in files.OrderBy(f => f.FullName))
            {
                var script = File.ReadAllText(file.FullName);

                try
                {
                    dl.ExecuteBatchQuery(script);
                }
                catch (Exception ex)
                {
                    throw new Exception(file.FullName, ex);
                }
            }
        }



        int getCurrentVersion()
        {
            int version = 0;

            try
            {
                version = Convert.ToInt32(dl.GetQueryScalar("SELECT Version FROM Settings"));
            }
            catch { }

            return version;
        }

        void setCurrentVersion(int version)
        {
            var query = String.Format("IF EXISTS(SELECT * FROM Settings) UPDATE Settings SET Version={0} WHERE Version<{0}; ELSE INSERT INTO Settings (Version) VALUES ({0})", version);
            dl.RunQuery(query);
        }

        string generateRandonSalt(int length)
        {
            Random random = new Random((int)DateTime.Now.Ticks);

            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

    }
}
