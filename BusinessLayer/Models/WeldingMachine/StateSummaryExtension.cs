using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.WeldingMachine
{
    public static class StateSummaryExtension
    {
        /// <summary>
        /// Форматировать сообщение, включая в него значения из состояния:
        /// Например, 'Значение тока = {State.I}'
        /// </summary>
        /// <param name="state"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        static public string FormatString(this StateSummary state, string format)
        {
            if (state == null || String.IsNullOrEmpty(format))
                return format;

            // Escape '{', '}'
            var message = format.Replace("{{", "||~~||").Replace("}}", "^^==^^");

            int pos1, pos2;
            while ((pos1 = message.IndexOf("{")) >= 0)
            {
                pos2 = message.IndexOf("}", pos1);
                if (pos2 < 0)
                    break;

                var val = "";

                // 'Test {State.I}'
                // pos1 = 5, pos2 = 13
                var pattern = message.Substring(pos1 + 1, pos2 - pos1 - 1); // PropertyCode

                if (!String.IsNullOrEmpty(pattern) && state.ContainsPropertyCode(pattern))
                {
                    val = state.GetRawValue(pattern);
                }

                message = message.Replace("{" + pattern + "}", val ?? "");
            }

            // Un-escape '{', '}'
            message = message.Replace("||~~||", "{").Replace("^^==^^", "}");

            return message;
        }
    }
}
