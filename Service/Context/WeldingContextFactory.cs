using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Context
{
    public class WeldingContextFactory : BusinessLayer.Interfaces.Context.IWeldingContextFactory
    {
        public WeldingContext CreateContext(int timeOutSecs)
        {
            var context = new DataLayer.Welding.WeldingContext(
                System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString,
                false
                );

            if (timeOutSecs > 0)
            {
                var objectContext = (context as System.Data.Entity.Infrastructure.IObjectContextAdapter).ObjectContext;
                objectContext.CommandTimeout = timeOutSecs; // 18000 - 5 hours
            }

            return context;
        }
    }
}
