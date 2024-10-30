using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Utils
{
    public class ReportsDateTimeFormats
    {
        /// <summary>
        /// Returns total time in format: HH:mm
        /// If negative: (HH:mm)
        /// </summary>
        static public string TotalTime(long secs)
        {
            var ts = TimeSpan.FromSeconds(Math.Abs(secs));

            var totalMinutes = (int)ts.TotalMinutes;

            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;

            var result = string.Format("{0}:{1}{2}", hours, mins < 10 ? "0" : "", mins);

            if (secs < 0)
                result = "(" + result + ")";

            return result;
        }

    }
}
