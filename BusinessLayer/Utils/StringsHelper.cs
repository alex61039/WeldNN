using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Utils
{
    public class StringsHelper
    {
        /// <summary>
        /// Converts string like 'A012FF' to byte[] array
        /// </summary>
        static public byte[] HexStringToByteArray(string hexString)
        {
            if (String.IsNullOrEmpty(hexString))
                return null;

            if (hexString.Length % 2 == 1)
                hexString = "0" + hexString;

            return Enumerable.Range(0, hexString.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(hexString.Substring(x, 2), 16))
                     .ToArray();
        }

        /// <summary>
        /// Converts byte[] array to string like
        /// </summary>
        static public string ByteArrayToHexString(byte[] arr)
        {
            if (arr == null || arr.Length == 0)
                return "";

            var r = "";
            for (var i = 0; i < arr.Length; i++)
                r += NumberToHexString(arr[i], 2);

            return r;
        }

        /// <summary>
        /// '01F0' -> 496
        /// </summary>
        static public int HexStringToNumber(string hexString)
        {
            try
            {
                return int.Parse(hexString, System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 496 => '01F0
        /// </summary>
        static public string NumberToHexString(int number, int len)
        {
            return len > 0 ? number.ToString("X" + len) : number.ToString("X");
        }
    }
}
