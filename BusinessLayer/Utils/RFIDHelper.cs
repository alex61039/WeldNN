using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Utils
{
    public class RFIDHelper
    {
        /// <summary>
        /// '191,09896' => 'BF26A8'
        /// </summary>
        /// <returns></returns>
        static public string Txt2Hex(string rfid_text)
        {
            if (String.IsNullOrEmpty(rfid_text))
                return null;

            var arr = rfid_text.Split(',');
            if (arr.Length != 2)
                return null;

            // 191, 9896
            int code1, code2;
            if (!Int32.TryParse(arr[0].Trim(), out code1))
                return null;
            if (!Int32.TryParse(arr[1].Trim(), out code2))
                return null;

            // BF, 26A8
            string hex1 = code1.ToString("X");
            string hex2 = code2.ToString("X");


            var codeHex = hex1 + hex2.PadLeft(4, '0');
            codeHex = codeHex.PadLeft(6, '0').ToUpper();
            
            return codeHex;
        }

        /// <summary>
        /// 'BF26A8' => '191,09896'
        /// </summary>
        /// <returns></returns>
        static public string Hex2Txt(string rfid_hex)
        {
            if (String.IsNullOrEmpty(rfid_hex))
                return null;

            rfid_hex = ("000000" + rfid_hex).Substring(rfid_hex.Length);    // take 6 most-right symbols

            string hex1 = rfid_hex.Substring(0, 2);
            string hex2 = rfid_hex.Substring(2, 4);

            int code1 = StringsHelper.HexStringToNumber(hex1);
            int code2 = StringsHelper.HexStringToNumber(hex2);

            return String.Format("{0},{1}", code1.ToString("D3"), code2.ToString("D5"));
        }


        /// <summary>
        /// Remove extra zeros, only 6 right characters
        /// </summary>
        /// <param name="rfid_hex"></param>
        /// <returns></returns>
        static public string Clean(string rfid_hex)
        {
            if (String.IsNullOrEmpty(rfid_hex) || rfid_hex.Length <= 6)
                return rfid_hex;

            return rfid_hex.Substring(rfid_hex.Length - 6);
        }
    }
}
