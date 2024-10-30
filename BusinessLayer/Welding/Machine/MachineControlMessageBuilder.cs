using BusinessLayer.Models.Configuration;
using BusinessLayer.Models.WeldingMachine;
using BusinessLayer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Machine
{
    public class MachineControlMessageBuilder
    {
        Models.Configuration.WeldingMachineTypeConfiguration config;

        public MachineControlMessageBuilder(Models.Configuration.WeldingMachineTypeConfiguration Config)
        {
            config = Config;
        }

        public string Build(
            DataLayer.Welding.WeldingMachine machine,
            Dictionary<string, ProgramControlItemValue> controlValues,
            BusinessLayer.Models.WeldingMachine.StateSummary state
            )
        {
            if (config == null || config.Outbound == null)
                return null;

            if (controlValues == null || controlValues.Count == 0)
                return null;


            var sb = new StringBuilder();

            // Header
            buildPart(sb, machine, config.Outbound.Header, controlValues, state);

            // Body
            buildPart(sb, machine, config.Outbound.Body, controlValues, state);


            // Add delimiter
            if (!String.IsNullOrEmpty(config.Outbound.Delimiter))
            {
                var tmp = new StringBuilder();
                tmp.Append(config.Outbound.Delimiter);
                tmp.Append(sb);
                tmp.Append(config.Outbound.Delimiter);

                sb = tmp;
            }

            return sb.ToString();
        }

        private void buildPart(StringBuilder sb,
            DataLayer.Welding.WeldingMachine machine,
            ICollection<DataProperty> properties,
            Dictionary<string, ProgramControlItemValue> controlValues,
            BusinessLayer.Models.WeldingMachine.StateSummary state)
        {
            // var part = new StringBuilder();
            var result = "";

            foreach (var p in properties)
            {
                // Constant values
                if (!String.IsNullOrEmpty(p.ConstantValue))
                {
                    result = setValueIntoMessagePart(result, padString(p.ConstantValue, p.Length), p.Start, p.Length, false);
                    // part.Append(padString(p.ConstantValue, p.Length));
                    continue;
                }

                // 
                string val = "";

                // By Property codes
                var propertyCode = p.PropertyCode;

                // Controls already contain the prop?
                if (controlValues.ContainsKey(propertyCode))
                {
                    // By default - just take the value
                    val = controlValues[propertyCode].Value;

                    // some different logic for ranges
                    switch (p.PropertyType)
                    {
                        case "range_min":
                        case "range_max":
                            {
                                // e.g. State.L
                                string controlledPropertyCode = p.RangeSource;

                                // Find the value/range of controlled 'State.L'
                                if (!String.IsNullOrEmpty(controlledPropertyCode) && controlValues.ContainsKey(controlledPropertyCode))
                                {
                                    var cpv = controlValues[controlledPropertyCode];
                                    if (cpv != null)
                                    {
                                        // As it is range - take Min or Max value
                                        var rangeValue = (p.PropertyType == "range_min")
                                            ? cpv.MinValue
                                            : cpv.MaxValue;

                                        val = buildHexStringByNumericValue(controlledPropertyCode, rangeValue);
                                    }
                                }

                                break;
                            }

                        case "number":
                            {
                                if (Double.TryParse(val, out double double_val))
                                    val = buildHexStringByNumericValue(p.PropertyCode, double_val);

                                break;
                            }
                    }
                    // /switch

                    // Case for StateCtrl
                    if (propertyCode == PropertyCodes.StateCtrl)
                    {
                        // Passive? Send same as Free (e.f. '02_passive')
                        if (!String.IsNullOrEmpty(val) && val.IndexOf("_passive") > 0)
                            val = val.Replace("_passive", "");
                    }
                }
                else
                {
                    // Some hardcoded rules
                    switch (propertyCode)
                    {
                        case PropertyCodes.CtrlMin:
                        case PropertyCodes.CtrlMax:
                        case PropertyCodes.CtrlMinL:
                        case PropertyCodes.CtrlMaxL:
                        case PropertyCodes.CtrlMinR:
                        case PropertyCodes.CtrlMaxR:
                            {
                                // Condition control - 'Ctrl.parm' or 'Ctrl.parmL' or 'Ctrl.parmR'
                                var conditionControlCode = (propertyCode == PropertyCodes.CtrlMin || propertyCode == PropertyCodes.CtrlMax) ? PropertyCodes.CtrlParm
                                    : (propertyCode == PropertyCodes.CtrlMinL || propertyCode == PropertyCodes.CtrlMaxL) ? PropertyCodes.CtrlParmL
                                    : PropertyCodes.CtrlParmR;


                                // Find what should be controlled
                                string conditionPropertyValue = controlValues.ContainsKey(conditionControlCode)
                                    ? controlValues[conditionControlCode].Value
                                    : "";

                                // E.g. controlledPropertyValue contains '1' or '2'
                                // Now find in 'Ctrl.parm' enums what it really controls, e.g. 'State.I'
                                string controlledPropertyCode = fetchEnumDescription(properties, conditionControlCode, conditionPropertyValue);

                                // Find the value/range of controlled 'State.I'
                                if (!String.IsNullOrEmpty(controlledPropertyCode) && controlValues.ContainsKey(controlledPropertyCode))
                                {
                                    var cpv = controlValues[controlledPropertyCode];
                                    if (cpv != null)
                                    {
                                        // As it is range - take Min or Max value
                                        var rangeValue = (propertyCode == PropertyCodes.CtrlMin || propertyCode == PropertyCodes.CtrlMinL || propertyCode == PropertyCodes.CtrlMinR)
                                            ? cpv.MinValue
                                            : cpv.MaxValue;

                                        val = buildHexStringByNumericValue(controlledPropertyCode, rangeValue);
                                    }
                                }

                                break;
                            }

                        case PropertyCodes.MACAddress:
                            {
                                val = machine.MAC;
                                break;
                            }

                        case PropertyCodes.CRC8:
                            {
                                // Build CRC only of current part (body) of message
                                // var bytes = Utils.StringsHelper.HexStringToByteArray(part.ToString());
                                var bytes = Utils.StringsHelper.HexStringToByteArray(result.Substring(0, p.Start));

                                var crc = Utils.CRCChecker.GET_CRC8(bytes, bytes.Length);

                                val = Utils.StringsHelper.NumberToHexString(crc, p.Length);
                                break;
                            }

                        case PropertyCodes.ServerDatetime:
                            {
                                val = Utils.WeldingDateTimeFormat.ToString(DateTime.Now);
                                break;
                            }

                        case PropertyCodes.PCount:
                            {
                                val = state != null 
                                    ? state.GetRawValue(PropertyCodes.PCount)
                                    : "0000";
                                break;
                            }

                        default:
                            {
                                val = "";

                                // some different logic for ranges
                                switch (p.PropertyType)
                                {
                                    case "range_min":
                                    case "range_max":
                                        {
                                            // e.g. State.L
                                            string controlledPropertyCode = p.RangeSource;

                                            // Find the value/range of controlled 'State.L'
                                            if (!String.IsNullOrEmpty(controlledPropertyCode) && controlValues.ContainsKey(controlledPropertyCode))
                                            {
                                                var cpv = controlValues[controlledPropertyCode];
                                                if (cpv != null)
                                                {
                                                    // As it is range - take Min or Max value
                                                    var rangeValue = (p.PropertyType == "range_min")
                                                        ? cpv.MinValue
                                                        : cpv.MaxValue;

                                                    val = buildHexStringByNumericValue(controlledPropertyCode, rangeValue);
                                                }
                                            }

                                            break;
                                        }

                                    case "number":
                                        {
                                            val = buildHexStringByNumericValue(p.PropertyCode, 0);

                                            break;
                                        }
                                }
                                // /switch

                                break;
                            }
                    }
                }


                // Append value, by specified length
                // part.Append(padString(val, p.Length));
                result = setValueIntoMessagePart(result, padString(val, p.Length), p.Start, p.Length, true);
            }

            // sb.Append(part);
            sb.Append(result);
        }

        string setValueIntoMessagePart(string part, string value, int position, int length, bool logicalOr)
        {
            var tmp = String.IsNullOrEmpty(part) ? "" : part;

            // Add zeros on the right
            if (tmp.Length < position + length)
            {
                tmp = tmp.PadRight(position + length, '0');
            }

            // cut the part
            var cut = tmp.Substring(position, length);

            // merge (logic OR)
            if (logicalOr)
            {
                var save = cut;
                try
                {
                    if (cut.Length > value.Length)
                        value = value.PadLeft(cut.Length, '0');
                    else if (cut.Length < value.Length)
                        cut = cut.PadLeft(value.Length, '0');

                    /*
                    byte[] bytes1 = Encoding.ASCII.GetBytes(cut);
                    byte[] bytes2 = Encoding.ASCII.GetBytes(value);
                    for(var i = 0; i < bytes1.Length; i++)
                    {
                        bytes1[i] = (byte)(bytes1[i] | bytes2[i]);
                    }

                    cut = Encoding.ASCII.GetString(bytes1);
                    */

                    var bytes1 = StringsHelper.HexStringToByteArray(cut);
                    var bytes2 = StringsHelper.HexStringToByteArray(value);

                    for (var i = 0; i < bytes1.Length; i++)
                    {
                        bytes1[i] = (byte)(bytes1[i] | bytes2[i]);
                    }

                    cut = StringsHelper.ByteArrayToHexString(bytes1);

                    if (cut.Length > length)
                        cut = cut.Substring(cut.Length - length, length);
                }
                catch
                {
                    // restore
                    cut = save;
                }
            }
            else
            {
                cut = value;
            }

            // put back
            var left = tmp.Substring(0, position);
            var right = tmp.Length > (position + length) ? tmp.Substring(position + length, tmp.Length - position - length) : "";
            tmp = left + cut + right;

            return tmp;
        }


        /// <summary>
        /// Enums: [ { Value: '1', Description: "first" } ]
        /// So, it should return 'first' by '1'
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="propertyCode"></param>
        /// <param name="enumValue"></param>
        /// <returns></returns>
        private string fetchEnumDescription(
            ICollection<DataProperty> properties,
            string propertyCode,
            string enumValue
            )
        {
            var p = properties.FirstOrDefault(pp => pp.PropertyCode == propertyCode);
            if (p == null || p.Enums == null)
                return null;

            var e = p.Enums.FirstOrDefault(ee => ee.Value == enumValue);

            return e.Description;
        }

        /// <summary>
        /// Apply rules like Multiplier, Negative, etc
        /// </summary>
        /// <returns></returns>
        private string buildHexStringByNumericValue(
            string PropertyCode,
            double value
            )
        {
            // default string value
            var result = Utils.StringsHelper.NumberToHexString((int)Math.Round(value), 0);

            // Lookup for rules in Inbound/Outbound part
            var p = config.Inbound.Body.FirstOrDefault(pp => pp.PropertyCode == PropertyCode);
            if (p == null)
            {
                p = config.Outbound.Body.FirstOrDefault(pp => pp.PropertyCode == PropertyCode);

                if (p == null)
                {
                    // No rules, convert to Whole Int
                    return result;
                }
            }

            // Really number type?
            if (p.PropertyType != "number" || p.Unit == null)
                return result;

            // Check multiplier
            if (p.Unit.Multiplier > 0)
            {
                // Divide and floor
                value = Math.Round(value / p.Unit.Multiplier);
            }

            // Negative/Positive rules
            if (p.Unit.Base > 0)
            {
                value = p.Unit.Base + value;
            }
            else if (p.Unit.NegativeBase > 0 && value < 0)
            {
                // -34 => 200 + 34 => 234
                value = p.Unit.NegativeBase + (-1 * value);
            }
            else if (p.Unit.PositiveBase > 0 && value >= 0)
            {
                // 24 => 100 + 24 => 124
                value = p.Unit.PositiveBase + value;
            }

            // Convert to hex-string
            result = Utils.StringsHelper.NumberToHexString((int)Math.Round(value), 0);

            return result;
        }

        /// <summary>
        /// Returns fixed-length string of '0'
        /// </summary>
        private string emptyData(int length)
        {
            return new string('0', length);
        }

        private string padString(string s, int len)
        {
            if (s == null) s = "";

            return s.PadLeft(len, '0');
        }
    }
}
