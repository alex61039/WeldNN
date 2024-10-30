using BusinessLayer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.WeldingMachine
{
    public class StateSummaryPropertyValue
    {
        public string PropertyCode { get; set; }
        public string Value { get; set; }
        public string PropertyType { get; set; }
        public string RawValue { get; set; }
        public bool LimitsExceeded { get; set; }
        public string LimitMin { get; set; }
        public string LimitMax { get; set; }
    }

    public class StateSummary
    {
        /// <summary>
        /// ID value from database table WeldingMachineState
        /// </summary>
        public int WeldingMachineStateID { get; set; }

        public DateTime DateCreated { get; set; }

        /// <summary>
        /// Null when the state if most first
        /// </summary>
        public DateTime? LastDatetimeUpdate { get; set; }

        public WeldingMachineStatus Status { get; set; }

        /// <summary>
        /// From Control/Program - Free, Limited, Passive, Block
        /// </summary>
        public String Control { get; set; }

        /// <summary>
        /// From Machine's state - Free, Limited, Passive, Block
        /// </summary>
        public String ControlState { get; set; }

        /// <summary>
        /// State duration in milliseconds
        /// </summary>
        public int StateDurationMs { get; set; }

        /// <summary>
        /// Error code in case of error state
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Welding Material
        /// </summary>
        public int? WeldingMaterialID { get; set; }

        /// <summary>
        /// Limits exceeded in Passive Mode?
        /// </summary>
        public bool LimitsExceeded { get; set; }

        /// <summary>
        /// Limit Program ID
        /// </summary>
        public int? WeldingLimitProgramID { get; set; }

        /// <summary>
        /// Limit Program Name. Or 'default'.
        /// </summary>
        public string WeldingLimitProgramName { get; set; }


        // public Dictionary<string, string> PropertyValues { get; set; }
        public Dictionary<string, StateSummaryPropertyValue> Properties { get; set; }


        /// <summary>
        /// Не пишется в базу. Только при постуалении пакета с аппарата
        /// </summary>
        public bool IsOfflineData { get; set; } = false;

        public StateSummary()
        {
            Properties = new Dictionary<string, StateSummaryPropertyValue>();
        }

        public bool ContainsPropertyCode(string PropertyCode)
        {
            return Properties != null && Properties.ContainsKey(PropertyCode);
        }

        public StateSummaryPropertyValue GetPropertyValue(string PropertyCode)
        {
            if (Properties == null)
                return null;

            if (Properties.ContainsKey(PropertyCode))
                return Properties[PropertyCode];

            return null;
        }

        /// <summary>
        /// Returns value or null if not exists.
        /// Case-sensitive
        /// </summary>
        public string GetRawValue(string PropertyCode)
        {
            if (Properties == null)
                return null;

            if (Properties.ContainsKey(PropertyCode))
                return Properties[PropertyCode].RawValue;

            return null;
        }

        public double GetNumericValue(string PropertyCode)
        {
            var val = GetRawValue(PropertyCode);

            if (String.IsNullOrEmpty(val))
                return 0;

            if (Double.TryParse(val, out double d))
                return d;

            return 0;
        }

        public int GetNumericFromHexValue(string PropertyCode)
        {
            try
            {
                var val = GetRawValue(PropertyCode);

                if (String.IsNullOrEmpty(val))
                    return 0;

                int int_val = StringsHelper.HexStringToNumber(val);

                return int_val;
            }
            catch { }

            return 0;
        }

        public string GetEnumValueDescription(string PropertyCode, ICollection<Configuration.EnumValue> enums)
        {
            var val = GetRawValue(PropertyCode);

            if (String.IsNullOrEmpty(val))
                return "";

            if (enums == null)
                return val;

            if (enums.Any(e => e.Value == val))
                return enums.FirstOrDefault(e => e.Value == val).Description;


            return val;
        }

        public string[] GetFlagsValueDescription(string PropertyCode, ICollection<Configuration.EnumValue> enums)
        {
            var list = new List<string>();

            try
            {
                var val = GetRawValue(PropertyCode);

                if (enums == null)
                    return new string[] { val };


                int int_val = StringsHelper.HexStringToNumber(val);

                foreach (var e in enums)
                {
                    try
                    {
                        var e_int = StringsHelper.HexStringToNumber(e.Value);

                        if ((int_val & e_int) > 0 || (int_val == 0 && e_int == 0))
                        {
                            list.Add(e.Description);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return list.ToArray();
        }

        public void SetValue(string PropertyCode, StateSummaryPropertyValue prop)
        {
            Properties[PropertyCode] = prop;
        }

        public void SetValue(string PropertyCode, string value, string type)
        {
            StateSummaryPropertyValue prop = null;

            if (Properties.ContainsKey(PropertyCode))
                prop = Properties[PropertyCode];

            // Add to dictionary if doesn't exist
            if (prop == null)
            {
                prop = new StateSummaryPropertyValue
                {
                    PropertyCode = PropertyCode,
                    PropertyType = type
                };

                Properties[PropertyCode] = prop;
            }

            // Set value
            prop.Value = value;
            prop.RawValue = value;
            prop.PropertyType = type;
        }

        public bool Equals(StateSummary obj)
        {
            if (obj == null) return false;

            // Both nulls?
            if (this.Properties == null && obj.Properties == null)
                return true;

            // One is null?
            if (this.Properties == null || obj.Properties == null)
                return false;

            // Controls
            if (!String.IsNullOrEmpty(this.Control) && !String.IsNullOrEmpty(obj.Control))
                if (this.Control != obj.Control)
                    return false;
            if (!String.IsNullOrEmpty(this.ControlState) && !String.IsNullOrEmpty(obj.ControlState))
                if (this.ControlState != obj.ControlState)
                    return false;
            if (!String.IsNullOrEmpty(this.ErrorCode) && !String.IsNullOrEmpty(obj.ErrorCode))
                if (this.ErrorCode != obj.ErrorCode)
                    return false;

            if (this.WeldingMaterialID != obj.WeldingMaterialID)
                return false;

            if (this.WeldingLimitProgramID != obj.WeldingLimitProgramID)
                return false;

            if (this.LimitsExceeded != obj.LimitsExceeded)
                return false;

            // Status
            if (this.Status != obj.Status)
                return false;

            // Different quantity of props
            if (this.Properties.Count != obj.Properties.Count)
                return false;

            // Both not nulls
            foreach (var p in this.Properties.Values)
            {
                // Doesn't contain a key
                if (!obj.Properties.ContainsKey(p.PropertyCode))
                    return false;

                // Value doesn't match
                if (!p.RawValue.Equals(obj.Properties[p.PropertyCode].RawValue))
                    return false;
            }

            return true;
        }
    }
}
