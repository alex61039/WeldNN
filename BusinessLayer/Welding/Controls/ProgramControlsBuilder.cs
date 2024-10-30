using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Controls
{
    public class ProgramControlsBuilder
    {
        Models.Configuration.WeldingMachineTypeConfiguration configuration;

        // Order of importance. All other properties witll go below
        string[] sortedImportantParameters = new string[]
        {
            Models.Configuration.PropertyCodes.StateCtrl,
            Models.Configuration.PropertyCodes.CtrlParm,
            Models.Configuration.PropertyCodes.CtrlParmL,
            Models.Configuration.PropertyCodes.CtrlParmR
        };

        public ProgramControlsBuilder(Models.Configuration.WeldingMachineTypeConfiguration Configuration)
        {
            configuration = Configuration;
        }

        public Models.WeldingMachine.ProgramControls BuildDefault()
        {
            var controls = new Models.WeldingMachine.ProgramControls
            {
                Name = "",
                Items = new List<Models.WeldingMachine.ProgramControlItem>(),
                WeldingMaterialID = 0
            };

            // First, for thru sorter partmeters
            foreach(var propertyCode in sortedImportantParameters)
            {
                var items = buildProgramControlItems(propertyCode);

                if (items != null)
                    controls.Items.AddRange(items);
            }

            // Them go thru all other controlled parameters
            foreach(var p in configuration.Outbound.Body)
            {
                // Empty property code?
                if (String.IsNullOrEmpty(p.PropertyCode))
                    continue;

                // Already processed
                if (sortedImportantParameters.Contains(p.PropertyCode))
                    continue;

                var items = buildProgramControlItems(p.PropertyCode);

                if (items != null)
                    controls.Items.AddRange(items);
            }

            return controls;
        }


        protected ICollection<Models.WeldingMachine.ProgramControlItem> buildProgramControlItems(string propertyCode)
        {
            // Skip some props
            if (propertyCode == Models.Configuration.PropertyCodes.CRC8)
                return null;

            var property = configuration.Outbound.Body.FirstOrDefault(pr => pr.PropertyCode == propertyCode);
            if (property == null)
                return null;

            // Prepare result
            var result = new List<Models.WeldingMachine.ProgramControlItem>();

            
            // First, some hardcoded params
            switch(property.PropertyCode)
            {
                case Models.Configuration.PropertyCodes.CtrlParm:
                case Models.Configuration.PropertyCodes.CtrlParmL:
                case Models.Configuration.PropertyCodes.CtrlParmR:
                    {
                        // Controlled parameters

                        // Create dropdown for the Control
                        var item = new Models.WeldingMachine.ProgramControlItem
                        {
                            ID = propertyCode,
                            Label = property.Description,
                            Type = Models.WeldingMachine.ProgramControlItemType.Option,
                            Options = new Dictionary<string, string>()
                        };

                        // options for the selection:
                        //  enums { Value, Description }
                        //      Value - is the value
                        //      Description - PropertyCode to control, e.g. 'State.I'
                        foreach(var e in property.Enums)
                        {
                            var key = e.Value;
                            var value = e.Description;  // PropertyCode

                            // lookup for property
                            if (configuration.Inbound.Body.Any(p => p.PropertyCode == value))
                                value = configuration.Inbound.Body.First(p => p.PropertyCode == value).Description;
                            else if (configuration.PropertyLimits.Limits.Any(l => l.PropertyCode == value))
                                value = configuration.PropertyLimits.Limits.First(l => l.PropertyCode == value).Description;

                            if (!String.IsNullOrEmpty(key) && !item.Options.ContainsKey(key))
                            {
                                item.Options.Add(key, value);
                            }
                                
                        }

                        result.Add(item);

                        // Add dependent parameters
                        foreach(var e in property.Enums)
                        {
                            var propCode = e.Description;   // Property Code

                            // look for limits
                            var limit = configuration.PropertyLimits.Limits.FirstOrDefault(l => l.PropertyCode == propCode);
                            if (limit != null)
                            {
                                // Show it only when selected in dropdown
                                var subItem = new Models.WeldingMachine.ProgramControlItem
                                {
                                    ID = propCode,
                                    Label = limit.Description,
                                    Type = Models.WeldingMachine.ProgramControlItemType.NumericRange,
                                    RangeMinValue = limit.MinValue,
                                    RangeMaxValue = limit.MaxValue,
                                    VisibilityItemID = propertyCode,
                                    VisibilityItemValue = e.Value,
                                    Step = limit.Step > 0 ? limit.Step : 1
                                };

                                result.Add(subItem);
                            }
                        }

                        break;
                    }
            }

            // Already built something?
            if (result.Count > 0)
                return result;

            
            // All others
            // Depending on type
            switch (property.PropertyType)
            {
                case "string":
                    // not supporting
                    return null;

                case "number":
                    {
                        // Number
                        var limit = configuration.PropertyLimits.Limits.FirstOrDefault(l => l.PropertyCode == propertyCode);
                        if (limit != null)
                        {
                            var item = new Models.WeldingMachine.ProgramControlItem
                            {
                                ID = propertyCode,
                                Label = String.IsNullOrEmpty(limit.Description) ? property.Description : limit.Description,
                                Type = Models.WeldingMachine.ProgramControlItemType.Number,
                                RangeMinValue = limit.MinValue,
                                RangeMaxValue = limit.MaxValue,
                                Step = limit.Step > 0 ? limit.Step : 1
                            };

                            result.Add(item);
                        }
                        break;
                    }

                case "enum":
                    {
                        var item = new Models.WeldingMachine.ProgramControlItem
                        {
                            ID = propertyCode,
                            Label = property.Description,
                            Type = Models.WeldingMachine.ProgramControlItemType.Option,
                            Options = property.Enums.ToDictionary(e => e.Value, e => e.Description)
                        };

                        result.Add(item);

                        break;
                    }

                case "flag":
                    {
                        var item = new Models.WeldingMachine.ProgramControlItem
                        {
                            ID = propertyCode,
                            Label = property.Description,
                            Type = Models.WeldingMachine.ProgramControlItemType.MultipleOption,
                            Options = property.Enums.ToDictionary(e => e.Value, e => e.Description)
                        };

                        result.Add(item);

                        break;
                    }

                case "range_min":
                    {
                        // do nothing for range_max
                        var rangePropertyCode = property.RangeSource;
                        if (String.IsNullOrEmpty(rangePropertyCode))
                            break;

                        // look for limits
                        var limit = configuration.PropertyLimits.Limits.FirstOrDefault(l => l.PropertyCode == rangePropertyCode);
                        if (limit != null)
                        {
                            // Show it only when selected in dropdown
                            var subItem = new Models.WeldingMachine.ProgramControlItem
                            {
                                ID = rangePropertyCode,
                                Label = limit.Description,
                                Type = Models.WeldingMachine.ProgramControlItemType.NumericRange,
                                RangeMinValue = limit.MinValue,
                                RangeMaxValue = limit.MaxValue,
                                Step = limit.Step > 0 ? limit.Step : 1
                            };

                            result.Add(subItem);
                        }

                        break;
                    }
            }


            return result;
        }
    }
}
