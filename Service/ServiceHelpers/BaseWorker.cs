using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService
{
    public abstract class BaseWorker
    {
        protected Thread m_thread;
        protected readonly EventWaitHandle m_exit;


        protected BaseWorker()
        {
            m_exit = new EventWaitHandle(false, EventResetMode.ManualReset);
        }


        private void Execute()
        {
            try
            {
                InternalExecute();
            }
            catch (Exception e)
            {
                Logger.LogException(e, "BaseWorker execute failed");
            }
        }


        protected abstract void InternalExecute();
        protected abstract void BeforeStop();


        public virtual void Start()
        {
            Logger.Log(LogLevel.Debug, Name + " starting...");

            m_thread = new Thread(Execute) { Name = Name };
            if (UseAbort)
                m_thread.IsBackground = true;
            m_thread.Start();

            Logger.Log(LogLevel.Debug, Name + " started.");
        }

        public virtual void Stop()
        {
            try
            {
                BeforeStop();
            }
            catch { }

            m_exit.Set();
            if (UseAbort)
                m_thread.Abort();
            else
                m_thread.Join();
        }


        protected abstract string Name { get; }

        protected virtual bool UseAbort
        {
            get { return false; }
        }

        protected DataLayer.Welding.WeldingContext GetWeldingContext()
        {
            return new DataLayer.Welding.WeldingContext(
                System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString, 
                false
                );
        }
    }
}
