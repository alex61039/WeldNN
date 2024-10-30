using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Interfaces.Context
{
    public interface IWeldingContextFactory
    {
        /// <summary>
        /// TimeOut - 0 default timeout
        /// </summary>
        /// <param name="timeOutSecs"></param>
        /// <returns></returns>
        DataLayer.Welding.WeldingContext CreateContext(int timeOutSecs);
    }
}
