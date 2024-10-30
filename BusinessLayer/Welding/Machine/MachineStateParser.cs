using BusinessLayer.Models.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Machine
{
    public class MachineStateParser
    {
        Models.Configuration.WeldingMachineTypeConfiguration config;

        public MachineStateParser(Models.Configuration.WeldingMachineTypeConfiguration Config)
        {
            config = Config;
        }

        public bool TryParse(string message, out Models.WeldingMachine.StateSummary state)
        {
            state = null;

            if (string.IsNullOrEmpty(message))
                return false;

            state = ParseMessageToState(message);

            // Parsed?
            return state == null ? false : true;
        }

        /// <summary>
        /// Parses network 'message' into State/Properties
        /// </summary>
        /// <param name="message">RAW Message from welding machine</param>
        /// <returns></returns>
        private Models.WeldingMachine.StateSummary ParseMessageToState(string message)
        {
            var inboundConfig = config.Inbound;

            // Split to Header and Body
            string header = "";
            string data = "";

            //
            if (inboundConfig.DataPropertiesOffset >= message.Length)
                return null;

            if (inboundConfig.DataPropertiesOffset > 0)
            {
                header = message.Substring(0, inboundConfig.DataPropertiesOffset);
                data = message.Substring(inboundConfig.DataPropertiesOffset);
            }
            else
            {
                // No header
                header = "";
                data = message;
            }

            // No need for header for now
            var stateHeader = ParseDataToState(header, inboundConfig.Header);

            // Parse Data part
            var state = ParseDataToState(data, inboundConfig.Body);

            // merge Header and Body props
            if (stateHeader != null && state != null)
            {
                foreach (var p in stateHeader.Properties.Values)
                {
                    if (!String.IsNullOrEmpty(p.PropertyCode))
                    {
                        state.SetValue(p.PropertyCode, p);
                    }
                }
            }

            // STATUSES, Errors
            if (state != null)
            {
                // Statuses (Сварка?)
                state.Status = retrieveWeldingStatus(state);

                // Error?
                state.ErrorCode = retrieveErrorCode(state);
                if (!String.IsNullOrEmpty(state.ErrorCode))
                {
                    state.Status = Models.WeldingMachineStatus.Error;
                }

                // Offline?
                state.IsOfflineData = IsOfflineDataMode(state);

                // State from Welding Machine (Free, Limited, Block)
                if (!state.IsOfflineData)
                {
                    state.ControlState = retrieveControlState(state);
                }
            }

            // Отладка - Сварка
            if (state != null && BusinessLayer.Configuration.TestingOptions.Instance.HasImmitate("Welding"))
            {
                // Имитация тока и напряжения
                double i = 180 + 60 * Math.Sin(DateTime.Now.Ticks / 10000000 / 3);
                double u = 30 + 10 * Math.Abs(Math.Cos(DateTime.Now.Ticks / 10000000 / 1));
                state.SetValue(PropertyCodes.I_Real, i.ToString(), "number");
                state.SetValue(PropertyCodes.U_Real, u.ToString(), "number");
            }


            return state;
        }

        Models.WeldingMachine.StateSummary ParseDataToState(string data, ICollection<Models.Configuration.DataProperty> dataPropertiesConfig)
        {
            if (String.IsNullOrEmpty(data))
                return null;

            bool valid = true;
            var state = new Models.WeldingMachine.StateSummary();

            foreach(var dp in dataPropertiesConfig)
            {
                // Check length
                if (data.Length < dp.Start + dp.Length)
                {
                    valid = false;
                    break;
                }

                string value = data.Substring(dp.Start, dp.Length);

                if (dp.Required && !String.IsNullOrEmpty(dp.ConstantValue))
                {
                    // Check constant values
                    if (value != dp.ConstantValue)
                    {
                        valid = false;
                        break;
                    }
                }
                else if (!String.IsNullOrEmpty(dp.PropertyCode)) {
                    // PropertyCode

                    switch(dp.PropertyCode)
                    {
                        case PropertyCodes.CRC8: {

                                // Validate CRC
                                try
                                {
                                    valid = Utils.CRCChecker.CRC8Validate(data);
                                }
                                catch (Exception ex)
                                {
                                    valid = false;
                                    throw new Exception("Exception on CRC8: " + data);
                                }

                                if (!valid)
                                {
                                    break;
                                }

                                break;
                            }

                        case PropertyCodes.Barcode: {
                                // EAN13
                                if (value.Length > 13)
                                    value = value.Substring(0, 13);

                                // Add VALUE to state
                                state.SetValue(dp.PropertyCode, value, dp.PropertyType);

                                break;
                            }

                        default: {
                                switch (dp.PropertyType)
                                {
                                    case "number":
                                        // parse value to number
                                        value = parseNumericValue(dp, value).ToString();
                                        break;

                                    case "flags":
                                        // flags are numeric
                                        value = parseNumericValue(dp, value).ToString();
                                        break;


                                }

                                // Add VALUE to state
                                state.SetValue(dp.PropertyCode, value, dp.PropertyType);

                                break;
                            }
                    }

                }


                // Break if not valid
                if (!valid)
                {
                    break;
                }
            }

            return valid ? state : null;
        }


        /// <summary>
        /// Оффлайновая дата?
        /// Проверяем есть ли дата с аппарата, и есть ли в конфиге блок OfflineDataMode, и валиден ли он
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool IsOfflineDataMode(Models.WeldingMachine.StateSummary state)
        {
            var result = false;

            if (config.ModeDefinitions != null && config.ModeDefinitions.OfflineDataMode != null
                && config.ModeDefinitions.OfflineDataMode.Conditions != null && config.ModeDefinitions.OfflineDataMode.Conditions.Count > 0)
            {
                var checker = new ConditionsSetChecker();

                result = checker.ValidateConditionsSet(state, config.ModeDefinitions.OfflineDataMode);
            }

            return result;
        }

        /// <summary>
        /// Control from STATE
        /// Free, Limited, Block
        /// </summary>
        string retrieveControlState(Models.WeldingMachine.StateSummary state)
        {
            var control = "";

            // Новый режим определения по ModeDefinitions
            if (config.ModeDefinitions != null)
            {
                // Если режимы определены в конфиге
                if ((config.ModeDefinitions.LimitedMode != null && config.ModeDefinitions.LimitedMode.Conditions != null && config.ModeDefinitions.LimitedMode.Conditions.Count > 0)
                    || (config.ModeDefinitions.BlockMode != null && config.ModeDefinitions.BlockMode.Conditions != null && config.ModeDefinitions.BlockMode.Conditions.Count > 0))
                {
                    var checker = new ConditionsSetChecker();

                    try
                    {

                        // Ограничения?
                        if (checker.ValidateConditionsSet(state, config.ModeDefinitions.LimitedMode))
                            control = "Limited";
                        // Блокировка?
                        else if (checker.ValidateConditionsSet(state, config.ModeDefinitions.BlockMode))
                            control = "Block";
                        // По умолчанию - Без ограничений
                        else
                            control = "Free";

                    }
                    catch (Exception ex)
                    {
                    }


                    return control;
                }
            }

            // Старый режим (MP) определения по State.ctrl
            /*
            if (state.ContainsPropertyCode(PropertyCodes.StateCtrl))
            {
                var prop = config.Inbound.Body.FirstOrDefault(p => p.PropertyCode == PropertyCodes.StateCtrl);
                if (prop != null)
                {
                    control = state.GetEnumValueDescription(PropertyCodes.StateCtrl, prop.Enums);
                }
            }
            */

            return control;
        }

        /// <summary>
        /// Определение состояния сварки
        /// </summary>
        Models.WeldingMachineStatus retrieveWeldingStatus(Models.WeldingMachine.StateSummary state)
        {
            // Отладка
            if (BusinessLayer.Configuration.TestingOptions.Instance.HasImmitate("Welding"))
                return Models.WeldingMachineStatus.Working;

            // Есть сообщение - значит как минимум включен
            var status = Models.WeldingMachineStatus.Ready;

            // Есть блок проверки режима в конфигурации?
            if (config.ModeDefinitions != null && config.ModeDefinitions.WeldingMode != null
                && config.ModeDefinitions.WeldingMode.Conditions != null && config.ModeDefinitions.WeldingMode.Conditions.Count > 0)
            {
                var checker = new ConditionsSetChecker();

                if (checker.ValidateConditionsSet(state, config.ModeDefinitions.WeldingMode))
                    status = Models.WeldingMachineStatus.Working;

                return status;
            }

            // В конфигураторе нет блока определения режима.
            // Определяем по старинке по параметрам

            // MX - State.sFlags
            if (state.ContainsPropertyCode(PropertyCodes.StateFlags))
            {
                var st = (int)state.GetNumericFromHexValue(PropertyCodes.StateFlags);
                if ((st & 64) != 0)
                    status = Models.WeldingMachineStatus.Working;
            }


            // MP - State.state бит 2 - сварка
            if (state.ContainsPropertyCode(PropertyCodes.State))
            {
                // Flag
                // var st = (int)state.GetNumericFromHexValue(PropertyCodes.State);
                var st = (int)state.GetNumericValue(PropertyCodes.State);
                if ((st & 2) != 0)
                    status = Models.WeldingMachineStatus.Working;
            }

            return status;
        }

        /// <summary>
        /// Код ошибки. Null, если нет ошибок.
        /// </summary>
        string retrieveErrorCode(Models.WeldingMachine.StateSummary state)
        {
            if (BusinessLayer.Configuration.TestingOptions.Instance.HasImmitate("Error"))
            {
                //var rnd = new Random();
                //return (rnd.Next(99) + 1).ToString();
                return "FF";
            }

            // Ищем код ошибки
            string err = null;

            // MX - State.errFlags
            if (state.ContainsPropertyCode(PropertyCodes.StateErr))
            {
                err = state.GetRawValue(PropertyCodes.StateErr);

                // only zeros?
                if (String.IsNullOrEmpty(err) || err.Replace("0", "") == "")
                    err = null;
            }

            // MP - State.state бит 3 (значение 4) - ошибка!
            // if (state.ContainsPropertyCode(PropertyCodes.State) && state.ContainsPropertyCode(PropertyCodes.StateError))
            if (state.ContainsPropertyCode(PropertyCodes.StateError))
            {
                err = state.GetRawValue(PropertyCodes.StateError);

                // only zeros?
                if (String.IsNullOrEmpty(err) || err.Replace("0", "") == "")
                    err = null;

                //var st = (int)state.GetNumericValue(PropertyCodes.State);
                //if ((st & 4) != 0)
                //    return state.GetRawValue(PropertyCodes.StateError);
            }

            
            // Есть блок проверки режима ошибки в конфигурации?
            if (config.ModeDefinitions != null && config.ModeDefinitions.ErrorMode != null 
                && config.ModeDefinitions.ErrorMode.Conditions != null && config.ModeDefinitions.ErrorMode.Conditions.Count > 0)
            {
                var checker = new ConditionsSetChecker();

                if (checker.ValidateConditionsSet(state, config.ModeDefinitions.ErrorMode))
                {
                    // Есть состояние ошибки

                    // Если код ошибки не найден - возвращаем константу
                    if (String.IsNullOrEmpty(err))
                        err = "FF";
                }
                else
                {
                    // Состоянии ошибки не детектировано
                    err = null;
                }
            }
            else
            {
                // Нет блока проверки состояния ошибки
                // Возвращаем код, если найден выше
            }


            return err;
        }


        double parseNumericValue(Models.Configuration.DataProperty dp, string stringValue)
        {
            // 1. Convert hex to number
            double numValue = (double)Utils.StringsHelper.HexStringToNumber(stringValue);

            // don't check 'flags' types
            if (dp.PropertyType == "number" && dp.Unit != null)
            {
                // 2. Rules about negative/positive?
                if (dp.Unit.Base > 0)
                {
                    // Base=50 => 85-50=35, 38-50=-12
                    numValue = numValue - dp.Unit.Base;
                }
                else if (dp.Unit.NegativeBase > 0 && dp.Unit.PositiveBase > 0)
                {
                    if (dp.Unit.NegativeBase > dp.Unit.PositiveBase)
                    {
                        // Negative base is GREATER than positive
                        if (numValue >= dp.Unit.NegativeBase)
                        {
                            // Negative
                            numValue = -1 * (numValue - dp.Unit.NegativeBase);
                        }
                        else if (numValue >= dp.Unit.PositiveBase)
                        {
                            // Positive
                            numValue = numValue - dp.Unit.PositiveBase;
                        }
                    }
                    else
                    {
                        // Positive base is GREATER than negative
                        if (numValue >= dp.Unit.PositiveBase)
                        {
                            // Positive
                            numValue = numValue - dp.Unit.PositiveBase;
                        }
                        else if (numValue >= dp.Unit.NegativeBase)
                        {
                            // Negative
                            numValue = -1 * (numValue - dp.Unit.NegativeBase);
                        }
                    }
                }

                // 3. Multiplier?
                if (dp.Unit.Multiplier > 0)
                {
                    numValue = Math.Round(numValue * dp.Unit.Multiplier, 4);
                }
            }

            return numValue;
        }
    }
}
