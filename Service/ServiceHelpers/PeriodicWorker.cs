using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.ServiceHelpers
{
    public class PeriodicWorker : BaseWorker
    {
        protected Action Action { get; set; }
        protected int PeriodMilliseconds { get; set; }
        protected bool SkipFirstRun { get; set; } = false;

        private int executionCounter = 0;

        public PeriodicWorker(int periodMilliseconds)
        {
            PeriodMilliseconds = periodMilliseconds;
            SkipFirstRun = false;
        }

        public PeriodicWorker(int periodMilliseconds, bool skipFirstRun)
        {
            PeriodMilliseconds = periodMilliseconds;
            SkipFirstRun = skipFirstRun;
        }

        public PeriodicWorker(Action action, int periodMilliseconds)
        {
            Action = action;
            PeriodMilliseconds = periodMilliseconds;
            SkipFirstRun = false;
        }


        protected override void InternalExecute()
        {
            while (true)
            {
                if (executionCounter > 0 || !SkipFirstRun)
                {
                    try
                    {
                        Action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Logger.LogException(e, "PeriodicWorker action failed");
                    }
                }

                var alreadySleep = 0;
                while (alreadySleep < PeriodMilliseconds)
                {
                    if (m_exit.WaitOne(1000))
                        return;
                    alreadySleep += 1000;
                }

                executionCounter++;
            }
        }

        protected override string Name
        {
            get { return "Periodic Worker"; }
        }

        protected override void BeforeStop()
        {
        }
    }
}
