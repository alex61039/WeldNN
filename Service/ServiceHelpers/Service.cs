using System;
using System.ServiceProcess;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService
{
    public class Service : ServiceBase
    {
        private readonly BaseWorker[] m_workers;

        public Service(BaseWorker[] workers)
        {
            m_workers = workers;
        }

        protected override void OnStart(string[] args)
        {
            foreach (var worker in m_workers)
            {
                worker.Start();
            }
        }

        protected override void OnStop()
        {
            RequestAdditionalTime(2000);

            foreach (var worker in m_workers)
            {
                worker.Stop();
            }
        }
    }
}
