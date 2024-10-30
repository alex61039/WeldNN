using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Threading.Tasks;
using System.Threading;

namespace WeldingService
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.Start(
                ConfigurationManager.AppSettings["LogDir"],
                ConfigurationManager.AppSettings["LogLevel"],
                Environment.UserInteractive
                );

            var culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            // Testing options
            BusinessLayer.Configuration.TestingOptions.Instance.Immitate = ConfigurationManager.AppSettings["Immitate"];

            // TODO
            // Wait for database

            // Create this object first, so all Objects should be loaded
            Workers.UpdatedObjectsChecker updatedObjectsChecker = new Workers.UpdatedObjectsChecker(5000);
            /*
            while (updatedObjectsChecker == null)
            {
                try
                {
                    updatedObjectsChecker = 
                    Logger.Log(LogLevel.Debug, "Created UpdatedObjectsChecker");
                }
                catch (Exception ex) {
                    Logger.LogException(ex, "Creating UpdatedObjectsChecker. Wating...");
                }

                // Wait
                Thread.Sleep(3000);
            }
            */

            // ============================================================================================
            try
            {
                Logger.Log(LogLevel.Debug, "Starting service workers...");

                ServiceHelper.Run(new BaseWorker[]
                {
                    // Checks for objects updates (Machines, Machine Types configurations, Organization Units, etc.)
                    updatedObjectsChecker,
                    // new Workers.UpdatedObjectsChecker(5000),

                    // TCP Listener. Accepts and sends messages to machines
                    new Workers.Listener(),

                    // Parser and dumper of incoming messsages from machines
                    new Workers.IncomingPacketsParser(ConfigurationManager.AppSettings["DumpIntoDB"] == "1"),

                    // Builder of outbound messages for machines
                    new Workers.OutboundPacketsWorker(100),

                    // Queue Tasks checker (reports creation, etc.)
                    new Workers.QueueTasksChecker(1000),

                    // Mailer
                    new Workers.MailerTask(),

                    // Update operating time for Welding Machines
                   // new Workers.MachineWorkingTime(5 * 60 * 1000, true)   // 5 minutes
                   new Workers.MachineWorkingTime(5 * 30 * 1000, false)   // 5 minutes
                }, args);

                Logger.Log(LogLevel.Debug, "All workers started.");
            }
            catch (Exception e)
            {
                Logger.LogException(e, "Worker start exception");
            }
            Logger.Stop();
        }
    }
}
