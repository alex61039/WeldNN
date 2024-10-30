using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Machine
{
    public class ConditionsSetChecker
    {
        public bool ValidateConditionsSet(
            Models.WeldingMachine.StateSummary state,
            Models.Configuration.IConditionsSet conditionsSet
            )
        {
            // No condition?
            if (conditionsSet == null || conditionsSet.Conditions == null || conditionsSet.Conditions.Count == 0)
                return true;

            // condition rule (AND by default)
            var rule = conditionsSet.ConditionsRule != "OR" ? "AND" : "OR";

            // prepare result
            var result = rule == "AND" ? true : false;

            // iterate conditions
            foreach (var c in conditionsSet.Conditions)
            {
                if (c == null)
                    continue;

                // validate condition
                var valid = validatePresentationCondition(state, c);

                result = rule == "AND" ? (result && valid) : (result || valid);

                if (rule == "AND" && !result)
                {
                    break;
                }

                if (rule == "OR" && result)
                {
                    break;
                }
            }

            if (conditionsSet.ConditionNegative)
                result = !result;

            return result;
        }

        protected bool validatePresentationCondition(
            Models.WeldingMachine.StateSummary state,
            Models.Configuration.PresentationCondition condition
            )
        {
            if (state == null || condition == null)
            {
                return false;
            }

            // Two cases for conditions - by PropertyCode, and by inner Conditions
            if (String.IsNullOrEmpty(condition.PropertyCode))
            {
                // Check inner conditions
                return ValidateConditionsSet(state, condition);
            }

            // =====================================================================
            // Check by PropertyCode

            // Get property value
            var p = state.GetPropertyValue(condition.PropertyCode);
            if (p == null)
                return false;

            string raw_value = p.RawValue;
            string property_type = p.PropertyType;

            if (raw_value == null)
                return false;


            var result = false;

            // prepare double values
            bool is_numeric = false;
            double double_value = 0;
            int int_value = 0;
            double condition_double_value = 0;
            int condition_int_value = 0;
            if ((new string[] { "lt", "gt", "lte", "gte" }).Contains(condition.Operator))
            {
                is_numeric = true;

                try
                {
                    if (property_type == "number" || property_type == "flags")
                        Double.TryParse(raw_value, out double_value);
                    else
                        double_value = Utils.StringsHelper.HexStringToNumber(raw_value);

                    // value - hex-base???
                    // is_numeric = Double.TryParse(raw_value, out double_value);

                    // condition.Value - 10-base
                    Double.TryParse(condition.Value, out condition_double_value);
                }
                catch
                {
                    is_numeric = false;
                }
            }
            if ((new string[] { "and" }).Contains(condition.Operator))
            {
                try
                {
                    is_numeric = true;

                    if (property_type == "number" || property_type == "flags")
                        Int32.TryParse(raw_value, out int_value);
                    else
                        int_value = Utils.StringsHelper.HexStringToNumber(raw_value);

                    // condition.Value - 10-base
                    Int32.TryParse(condition.Value, out condition_int_value);

                    // throw new Exception(String.Format("\n\n!!!! {0} - {1} - {2} |||| {3} - {4}\n\n", property_type, raw_value, int_value, condition.Value, condition_int_value));
                }
                catch
                {
                    is_numeric = false;
                }
            }

            switch (condition.Operator)
            {
                // equals
                case "e":
                    result = condition.Value.Equals(raw_value, StringComparison.InvariantCulture);
                    break;

                // not equals
                case "ne":
                    result = !condition.Value.Equals(raw_value, StringComparison.InvariantCulture);
                    break;

                // less than
                case "lt":
                    result = is_numeric && double_value < condition_double_value;
                    break;

                // greater than
                case "gt":
                    result = is_numeric && double_value > condition_double_value;
                    break;

                // less than or equal
                case "lte":
                    result = is_numeric && double_value <= condition_double_value;
                    break;

                // greater than or equal
                case "gte":
                    result = is_numeric && double_value >= condition_double_value;
                    break;

                // has bit (flag), bitwise 'and'
                case "and":
                    if (is_numeric)
                    {
                        result = (int_value & condition_int_value) == condition_int_value;
                    }
                    break;
            }

            if (condition.ConditionNegative)
                result = !result;

            return result;
        }
    }
}
