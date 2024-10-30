using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Utils
{
    public class WeldingDateTimeFormat
    {
        static public DateTime? Parse(string val)
        {
            if (String.IsNullOrEmpty(val))
                return null;

            try
            {
                var arr = StringsHelper.HexStringToByteArray(val);
                if (arr.Length != 6)
                    return null;

                var dt = new DateTime(
                    arr[0] + 2000,      // year
                    arr[1],             // month
                    arr[2],             // day
                    arr[3],             // hour
                    arr[4],             // minute
                    arr[5]             // second
                    );

                return dt;
            }
            catch (Exception ex) {
                return null;
            }
        }


        static public String ToString(DateTime dt)
        {
            // Array of bytes:
            // year (last 2 digits)
            // month
            // day
            // hour
            // minute
            // second

            var arr = new byte[6];
            arr[0] = (byte)(dt.Year - 2000);
            arr[1] = (byte)dt.Month;
            arr[2] = (byte)dt.Day;
            arr[3] = (byte)dt.Hour;
            arr[4] = (byte)dt.Minute;
            arr[5] = (byte)dt.Second;

            var result = StringsHelper.ByteArrayToHexString(arr);

            return result;
        }
    }
}
