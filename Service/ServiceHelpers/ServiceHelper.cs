using System;
using System.ServiceProcess;

namespace WeldingService
{
    public static class ServiceHelper
    {
        public static void Run(BaseWorker worker, params string[] args)
        {
            Run(new[] { worker }, args);
        }

        public static void Run(BaseWorker[] workers, params string[] args)
        {
            bool isConsole = false;

            var cmd = String.Empty;
            if (args.Length > 0)
                cmd = args[0];
            switch (cmd)
            {
                case "/console":
                    isConsole = true;
                    break;
            }

            // RUN
            if (isConsole || Environment.UserInteractive)
            {
                Logger.Log(LogLevel.Notice, "Service console started");

                // Console
                foreach (var worker in workers)
                {
                    worker.Start();
                }

                Console.WriteLine("Press enter to terminate.");
                Console.ReadLine();

                foreach (var worker in workers)
                {
                    worker.Stop();
                }
            }
            else
            {
                Logger.Log(LogLevel.Notice, "Service started");

                // Service
                ServiceBase.Run(new Service(workers));
            }
        }
    }
}
