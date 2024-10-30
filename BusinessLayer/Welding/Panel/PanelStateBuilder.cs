using BusinessLayer.Models.Configuration;
using BusinessLayer.Models.WeldingMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Panel
{
    public class PanelStateBuilder
    {
        Models.Configuration.WeldingMachineTypeConfiguration configuration;
        DataLayer.Welding.WeldingContext context;
        BusinessLayer.Welding.Machine.ConditionsSetChecker conditionsChecker;

        public PanelStateBuilder(
            Models.Configuration.WeldingMachineTypeConfiguration Configuration,
            DataLayer.Welding.WeldingContext Context
            )
        {
            configuration = Configuration;
            context = Context;
            conditionsChecker = new Machine.ConditionsSetChecker();
        }

        public Models.WeldingMachine.PanelState Build(Models.WeldingMachine.StateSummary state)
        {
            var panelState = new Models.WeldingMachine.PanelState();

            // Empty state, or missing Presentation configuration?
            if (state == null || configuration == null || configuration.Presentation == null)
                return panelState;

            panelState.LastDatetimeUpdate = state.LastDatetimeUpdate.GetValueOrDefault();

            // Presentation: Panel items
            // Iterate configuration
            foreach (var p in configuration.Presentation.Properties)
            {
                // Validate conditions
                var valid = conditionsChecker.ValidateConditionsSet(state, p);

                if (!valid)
                    continue;

                // condition is valid
                switch (p.Display)
                {
                    case "text":
                        {
                            string textValue = "";

                            // Property or Constant value?
                            if (!String.IsNullOrEmpty(p.PropertyCode))
                            {
                                var rawValue = state.GetRawValue(p.PropertyCode);
                                textValue = rawValue;

                                // Number?
                                if (Double.TryParse(rawValue, out double tmpDouble))
                                {
                                    // by default format is whole: 00
                                    textValue = tmpDouble.ToString("F0");

                                    if (p.TextStyle.NumberMultiplier > 0 || p.TextStyle.NumberDigits > 0)
                                    {
                                        // Multiplier
                                        if (p.TextStyle.NumberMultiplier > 0)
                                            tmpDouble = tmpDouble * p.TextStyle.NumberMultiplier;

                                        // Digits
                                        if (p.TextStyle.NumberDigits > 0)
                                        {
                                            textValue = tmpDouble.ToString("F" + p.TextStyle.NumberDigits);
                                        }
                                        else
                                        {
                                            textValue = tmpDouble.ToString();
                                        }
                                    }
                                }
                            }
                            else if (!String.IsNullOrEmpty(p.ConstantText))
                            {
                                textValue = p.ConstantText;
                            }

                            var panelItem = new Models.WeldingMachine.PanelTextItem
                            {
                                X = p.Offset.X,
                                Y = p.Offset.Y,
                                Color = p.Color,
                                FontFamily = p.TextStyle != null ? p.TextStyle.FontFamily : null,
                                FontSize = p.TextStyle != null ? p.TextStyle.FontSize.GetValueOrDefault() : 0,
                                FontStyle = p.TextStyle != null ? p.TextStyle.FontStyle : null,
                                Text = textValue
                            };

                            panelState.Texts.Add(panelItem);
                            break;
                        }

                    case "led":
                        {
                            var panelItem = new Models.WeldingMachine.PanelLedItem
                            {
                                X = p.Offset.X,
                                Y = p.Offset.Y,
                                Color = p.Color,
                                Width = p.LedStyle != null ? p.LedStyle.Width : 0,
                                Height = p.LedStyle != null ? p.LedStyle.Height : 0,
                                Radius = p.LedStyle != null ? p.LedStyle.Radius.GetValueOrDefault() : 0
                            };

                            panelState.Leds.Add(panelItem);
                            break;
                        }
                }
            }

            // Summary properties
            panelState.SummaryProperties = BuildSummaryProperties(state, false);

            // Worker
            panelState.WorkerInfo = buildWorkerInfo(state);

            // I, U
            if (state.Status == Models.WeldingMachineStatus.Working)
            {
                panelState.IReal = state.GetNumericValue(PropertyCodes.I_Real);
                panelState.UReal = state.GetNumericValue(PropertyCodes.U_Real);
            }

            return panelState;
        }

        /// <summary>
        /// List of all ShowSummary properties.
        /// With values, if state is not null.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public List<Models.WeldingMachine.SummaryProperty> BuildSummaryProperties(
            Models.WeldingMachine.StateSummary state,
            bool includeNotShowInSummary
            )
        {
            var list = new List<Models.WeldingMachine.SummaryProperty>();

            if (state != null)
            {
                // Статус состояния: Выключен, Включен, Сварка, Ошибка
                list.Add(new SummaryProperty
                {
                    PropertyCode = "Custom.Status",
                    Title = "Status",
                    Value = state.Status == Models.WeldingMachineStatus.Off ? ((int)Models.WeldingMachineStatus.Off).ToString()
                        : ((state.Status == Models.WeldingMachineStatus.Ready || state.Status == Models.WeldingMachineStatus.Working) && !String.IsNullOrEmpty(state.ErrorCode)) ? ((int)Models.WeldingMachineStatus.Error).ToString()
                        : state.Status == Models.WeldingMachineStatus.Working ? ((int)Models.WeldingMachineStatus.Working).ToString()
                        : state.Status == Models.WeldingMachineStatus.Service ? ((int)Models.WeldingMachineStatus.Service).ToString()
                        : state.Status == Models.WeldingMachineStatus.Error ? ((int)Models.WeldingMachineStatus.Error).ToString()
                        : (state.Status == Models.WeldingMachineStatus.Ready && String.IsNullOrEmpty(state.ErrorCode)) ? ((int)Models.WeldingMachineStatus.Ready).ToString()
                        : ""
                });

                // Режим управления: Без ограничений, Пассивный, Ограничения, Блокировка
                if (state.Status == Models.WeldingMachineStatus.Ready || state.Status == Models.WeldingMachineStatus.Working || state.Status == Models.WeldingMachineStatus.Error)
                {
                    list.Add(new SummaryProperty
                    {
                        PropertyCode = "Custom.Control",
                        Title = "Control",
                        Value = state.ControlState == "Block" ? "Block"
                            : state.ControlState == "Limited" ? "Limited"
                            : (state.ControlState == "Passive" || state.Control == "Passive") ? "Passive"
                            : "Free"
                    });
                }
            }

            // Checker for ShowInSummaryConditions
            var checker = new Machine.ConditionsSetChecker();

            // Остальные свойства из конфига
            var allProps = new List<DataProperty>();
            allProps.AddRange(configuration.Inbound.Header);
            allProps.AddRange(configuration.Inbound.Body);
            foreach (var p in allProps)
            {
                if (!p.ShowInSummary)
                {
                    if (!includeNotShowInSummary)
                    {
                        continue;
                    }

                    // do not show props without description
                    if (String.IsNullOrEmpty(p.Description))
                    {
                        continue;
                    }
                }

                // check ShowInSummaryConditions (when includeNotShowInSummary==false)
                if (p.ShowInSummaryConditions?.Conditions != null && p.ShowInSummaryConditions.Conditions.Count > 0 && !includeNotShowInSummary)
                {
                    if (!checker.ValidateConditionsSet(state, p.ShowInSummaryConditions))
                        continue;

                }

                // Title and Value
                var sp = new Models.WeldingMachine.SummaryProperty
                {
                    PropertyCode = p.PropertyCode,
                    Title = p.Description,
                    Value = "",
                    PropertyType = p.PropertyType
                };

                // Value
                if (state != null)
                {
                    switch (p.PropertyType)
                    {
                        case "number":
                            sp.Value = state.GetNumericValue(p.PropertyCode).ToString();
                            break;

                        case "enum":
                            sp.Value = state.GetEnumValueDescription(p.PropertyCode, p.Enums);
                            break;

                        case "flags":
                            sp.Value = String.Join(", ", state.GetFlagsValueDescription(p.PropertyCode, p.Enums));
                            break;

                        default:
                            sp.Value = state.GetRawValue(p.PropertyCode);
                            break;
                    }
                }

                // Unit
                if (p.Unit != null && !String.IsNullOrEmpty(p.Unit.UnitCode))
                {
                    sp.Unit = p.Unit.UnitCode;
                }

                // Specific cases
                switch (p.PropertyCode)
                {
                    case PropertyCodes.RFID:
                        var rfid_text = Utils.RFIDHelper.Hex2Txt(sp.Value);
                        if (!String.IsNullOrEmpty(rfid_text))
                            // sp.Value = String.Format("{0} ({1})", sp.Value, rfid_text);
                            sp.Value = rfid_text;
                        break;
                }

                list.Add(sp);
            }


            return list;
        }

        public Models.WeldingMachine.PanelState Immitate()
        {
            var panelState = new Models.WeldingMachine.PanelState
            {
                LastDatetimeUpdate = DateTime.Now
            };

            // Randomly show leds, text
            Random rnd = new Random();

            foreach (var p in configuration.Presentation.Properties)
            {
                // show prop or not?
                if (rnd.NextDouble() < 0.3)
                    continue;

                if (p.Display == "text")
                {
                    // Do not show text with same coordinates
                    if (panelState.Texts.Any(t => t.X == p.Offset.X && t.Y == p.Offset.Y))
                        continue;
                }

                switch (p.Display)
                {
                    case "text":
                        {
                            string textValue = "";

                            // Property code or constant text?
                            if (!String.IsNullOrEmpty(p.ConstantText))
                            {
                                textValue = p.ConstantText;
                            }
                            else
                            {
                                // random number
                                string rawValue = (rnd.NextDouble() * 100).ToString("F1");
                                textValue = rawValue;

                                // Number?
                                if (Double.TryParse(rawValue, out double tmpDouble))
                                {
                                    // by default format is whole
                                    textValue = tmpDouble.ToString("F0");

                                    if (p.TextStyle.NumberMultiplier > 0 || p.TextStyle.NumberDigits > 0)
                                    {
                                        // Multiplier
                                        if (p.TextStyle.NumberMultiplier > 0)
                                            tmpDouble = tmpDouble * p.TextStyle.NumberMultiplier;

                                        // Digits
                                        if (p.TextStyle.NumberDigits > 0)
                                        {
                                            textValue = tmpDouble.ToString("F" + p.TextStyle.NumberDigits);
                                        }
                                        else
                                        {
                                            textValue = tmpDouble.ToString();
                                        }
                                    }
                                }
                            }


                            var panelItem = new Models.WeldingMachine.PanelTextItem
                            {
                                X = p.Offset.X,
                                Y = p.Offset.Y,
                                Color = p.Color,
                                FontFamily = p.TextStyle != null ? p.TextStyle.FontFamily : null,
                                FontSize = p.TextStyle != null ? p.TextStyle.FontSize.GetValueOrDefault() : 0,
                                FontStyle = p.TextStyle != null ? p.TextStyle.FontStyle : null,
                                Text = textValue
                            };

                            panelState.Texts.Add(panelItem);
                            break;
                        }
                    case "led":
                        {
                            var panelItem = new Models.WeldingMachine.PanelLedItem
                            {
                                X = p.Offset.X,
                                Y = p.Offset.Y,
                                Color = p.Color,
                                Width = p.LedStyle != null ? p.LedStyle.Width : 0,
                                Height = p.LedStyle != null ? p.LedStyle.Height : 0,
                                Radius = p.LedStyle != null ? p.LedStyle.Radius.GetValueOrDefault() : 0
                            };

                            panelState.Leds.Add(panelItem);
                            break;
                        }
                }
            }

            return panelState;
        }



        // ======================================================================================

        /// <summary>
        /// Инфо о сварщике
        /// </summary>
        /// <returns></returns>
        PanelWorkerInfo buildWorkerInfo(Models.WeldingMachine.StateSummary state)
        {
            if (!state.ContainsPropertyCode(PropertyCodes.RFID))
                return null;

            var rfid = state.GetRawValue(PropertyCodes.RFID);
            if (String.IsNullOrEmpty(rfid))
                return null;

            PanelWorkerInfo result = null;

            var accountsManager = new Accounts.AccountsManager(context);
            var userAccount = accountsManager.FindByRFID(rfid);

            if (userAccount != null)
            {
                result = new PanelWorkerInfo {
                    UserAccountID = userAccount.ID,
                    Name = userAccount.Name,
                    Photo = userAccount.Photo.HasValue ? userAccount.Photo.ToString() : null
                };
            }

            return result;
        }

    }
}
