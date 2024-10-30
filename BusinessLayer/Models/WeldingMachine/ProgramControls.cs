using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.WeldingMachine
{
    public class ProgramControls
    {
        /// <summary>
        /// Null or Empty for default program
        /// </summary>
        public string Name { get; set; }
        public int WeldingMachineTypeID { get; set; }

        public List<ProgramControlItem> Items { get; set; }

        public int WeldingMaterialID { get; set; }
    }

    public class ProgramControlItem
    {
        public string ID { get; set; }

        public string Label { get; set; }

        public ProgramControlItemType Type { get; set; }

        public Dictionary<string, string> Options { get; set; }

        public double RangeMinValue { get; set; }
        public double RangeMaxValue { get; set; }
        public double Step { get; set; }

        public string VisibilityItemID { get; set; }
        public string VisibilityItemValue { get; set; }
    }

    public class ProgramControlItemValue
    {
        public string ID { get; set; }
        public string Value { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
    }

    public enum ProgramControlItemType
    {
        Option = 1,
        MultipleOption = 2,
        NumericRange = 3,
        Number = 4
    }
}
