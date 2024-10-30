using BusinessLayer.Models;
using BusinessLayer.Models.Configuration;
using BusinessLayer.Models.WeldingMachine;
using DataLayer.Welding;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Data.Entity;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Workers
{
    public class IncomingPacketsParser : BaseWorker
    {
        private bool dumpIntoDB;
        // private DataLayer.Welding.WeldingContext context;
        // private BusinessLayer.Welding.Machine.MachineStateService machineStateService;

        protected override string Name => "Messages dump into db";

        public IncomingPacketsParser(bool DumpIntoDB) : base()
        {
            dumpIntoDB = DumpIntoDB;
        }

        protected override void BeforeStop()
        {
            try
            {
                // context.Dispose();
            }
            catch { }
        }

        protected override void InternalExecute()
        {
            do
            {
                // Check queue
                Models.Packet packet;
                while (Domain.IncomingPacketsQueue.TryDequeue(out packet) && packet != null)
                {
                    try
                    {
                        // Packet fetched
                        processPacket(packet);

                        // Dump
                        if (dumpIntoDB)
                        {
                            dump(packet);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "IncomingPacketsQueue.InternalExecute: " + ex.Message);
                    }

                    if (m_exit.WaitOne(10))
                        return;

                }

                if (m_exit.WaitOne(10))
                    return;

            } while (true);
        }


        void processPacket(Models.Packet packet)
        {
            // No data or no MAC?
            if (String.IsNullOrEmpty(packet.Data) || String.IsNullOrEmpty(packet.MAC))
                return;

            // Get config
            if (!Domain.DomainObjects.ConfigsByMAC.ContainsKey(packet.MAC))
                return;

            var config = Domain.DomainObjects.ConfigsByMAC[packet.MAC];
            if (config == null)
                return;

            // Find Welding machine
            if (!Domain.DomainObjects.WeldingMachinesByMAC.ContainsKey(packet.MAC))
                return;
            var weldingMachine = Domain.DomainObjects.WeldingMachinesByMAC[packet.MAC];
            if (weldingMachine == null)
                return;

            // Divide packet to messages
            string[] messages = null;

            if (String.IsNullOrEmpty(config.Inbound.Delimiter))
                messages = new string[] { packet.Data };
            else
                messages = packet.Data.Split(new string[] { config.Inbound.Delimiter }, StringSplitOptions.RemoveEmptyEntries);

            if (messages == null || messages.Length == 0)
                return;

            // Parse messages
            var parser = new BusinessLayer.Welding.Machine.MachineStateParser(config);

            foreach (var message in messages)
            {
                if (!processMessage(parser, weldingMachine.ID, weldingMachine.MAC, message, config))
                {
                    Logger.Log(LogLevel.Warning, "{0} - ошибка разбора пакета от аппарата", packet.MAC);
                }
            }
        }

        bool processMessage(
            BusinessLayer.Welding.Machine.MachineStateParser parser,
            int WeldingMachineID,
            string WeldingMachineMAC,
            string message,
            WeldingMachineTypeConfiguration config
            )
        {
            // Validate message and convert to State
            if (parser.TryParse(message, out BusinessLayer.Models.WeldingMachine.StateSummary state) && state != null)
            {

                // Save state
                using (var context = GetWeldingContext())
                {
                    // Fetch current Control - from Limits/Program

                    // Применить значения из текущего управления/программы:
                    // вид управления (без ограничений, пассивный и т.п.), сварочный материал и т.д.
                    // В случае Пассивного управления - проверить выходы за пределы
                    state = ApplyControlPrograms(context, WeldingMachineID, state, config);

                    // No Control comes from machine (e.g. MX) - use Control from Limits/Program - ONLY Passive!
                    if (String.IsNullOrEmpty(state.ControlState) && state.Control == "Passive")
                        state.ControlState = state.Control;


                    // Сохранить state в кэш
                    Domain.MachineStatesRepository.Set(WeldingMachineMAC, state);

                    // Ошибка - создать уведомление
                    if (!state.IsOfflineData && state.Status == WeldingMachineStatus.Error)
                    {
                        createWeldingErrorNotification(context, WeldingMachineID, state);
                    }

                    // Save State
                    var machineStateService = new BusinessLayer.Welding.Machine.MachineStateService(context);
                    machineStateService.SaveState(WeldingMachineID, state);

                    return true;
                }

            }

            return false;
        }

        void createWeldingErrorNotification(
            DataLayer.Welding.WeldingContext context,
            int WeldingMachineID,
            BusinessLayer.Models.WeldingMachine.StateSummary state)
        {
            if (state == null || state.Status != WeldingMachineStatus.Error || String.IsNullOrEmpty(state.ErrorCode))
                return;

            var machine = context.WeldingMachines.Include(m => m.OrganizationUnit).FirstOrDefault(m => m.ID == WeldingMachineID);
            if (machine == null)
                return;

            // Create notification
            var notificationParameters = new BusinessLayer.Models.Notifications.NotificationTypeWeldingError.NotificationWeldingErrorParameters
            {
                WeldingMachineID = machine.ID,
                WeldingMachineName = machine.Name,
                WeldingMachineLabel = machine.Label,
                WeldingMachineMAC = machine.MAC,
                AlertDatetime = DateTime.Now,
                ErrorCode = state.ErrorCode
            };

            var notification = new BusinessLayer.Models.Notifications.NotificationTypeWeldingError(notificationParameters);

            // Save notification
            var notificationService = new BusinessLayer.Services.Notifications.NotificationsService(context);
            notificationService.SaveByUserPermission(notification, "NotificationWeldingError", machine.OrganizationUnit.OrganizationID);
        }

        /// <summary>
        /// Control from Control/Program: Free, Limited, Passive, Block.
        /// For 'Passive' mode - check limits and create Notifications
        /// </summary>
        BusinessLayer.Models.WeldingMachine.StateSummary ApplyControlPrograms(
            DataLayer.Welding.WeldingContext context,
            int weldingMachineID,
            BusinessLayer.Models.WeldingMachine.StateSummary state,
            WeldingMachineTypeConfiguration config)
        {
            var dataPropertiesConfig = config.Inbound.Body;

            // Load control
            var programControlsService = new BusinessLayer.Welding.Controls.ProgramControlsService(context);
            var currentProgramID = programControlsService.GetCurrentWeldingMachineProgramID(weldingMachineID);

            var currentProgram = programControlsService.GetWeldingMachineProgram(currentProgramID);
            if (currentProgram == null || currentProgram.ProgramStatus != (int)GeneralStatus.Active)
                return state;

            // Set Program
            state.WeldingLimitProgramID = currentProgram.ID;
            state.WeldingLimitProgramName = currentProgram.IsMachineDefault ? "default" : currentProgram.Name;

            // Свариваемый материал
            state.WeldingMaterialID = currentProgram.WeldingMaterialID;


            // ==========================================================================================================================
            // Далее только для онлайн-данных
            if (state.IsOfflineData)
            {
                return state;
            }


            var dictProgramValues = programControlsService.LoadWeldingMachineProgramValues(currentProgramID);

            if (dictProgramValues == null)
                return state;

            // Free, Passive, Limited, Block
            var control = "";

            //Logger.Log(LogLevel.Debug, dictProgramValues.ContainsKey(PropertyCodes.StateCtrl) ? "Contains" : "NOT Contains");
            //string tmp = String.Format("currentProgramID = {0}\n", currentProgramID);
            //tmp += String.Format("weldingMachineID = {0}\n", weldingMachineID);
            //foreach (var kv in dictProgramValues)
            //{
            //    tmp += String.Format("{0} - {1}\n", kv.Key, kv.Value);
            //}
            //tmp += "\n\n";
            //Logger.Log(LogLevel.Debug, tmp);

            var controlProgramParameter = dictProgramValues.ContainsKey(PropertyCodes.StateCtrl)
                ? dictProgramValues[PropertyCodes.StateCtrl]
                : null;

            if (controlProgramParameter != null)
            {
                // Take prop with enums from config
                var prop = config.Inbound.Body.FirstOrDefault(p => p.PropertyCode == PropertyCodes.StateCtrl);
                if (prop == null)
                    prop = config.Outbound.Body.FirstOrDefault(p => p.PropertyCode == PropertyCodes.StateCtrl);

                if (prop != null)
                {
                    // Passive?
                    var v = controlProgramParameter.Value;
                    if (!String.IsNullOrEmpty(v) && v.ToLower().IndexOf("passive") >= 0)
                    {
                        control = "Passive";
                    }
                    else
                    {
                        var e = prop.Enums.FirstOrDefault(ee => ee.Value == v);
                        control = e.Description;
                    }
                }
            }

            state.Control = control;


            // Пассивный режим: проверить выходы за пределы.
            // Только в режиме сварки.
            // Только не в оффлайн
            if (control == "Passive" && state.Status == BusinessLayer.Models.WeldingMachineStatus.Working)
            // if (control == "Passive")
            {
                checkPassiveLimitsAndCreateNotifications(context, weldingMachineID, state, dictProgramValues, config);
            }

            // Уведомления от сварочного аппарата
            checkWeldingMachineAlerts(context, weldingMachineID, state, config);

            return state;
        }


        /// <summary>
        /// Уведомления от сварочных аппрататов по конфигурации, независимо от режима работы
        /// </summary>
        void checkWeldingMachineAlerts(
            DataLayer.Welding.WeldingContext context,
            int weldingMachineID,
            BusinessLayer.Models.WeldingMachine.StateSummary state,
            WeldingMachineTypeConfiguration config
            )
        {
            if (state == null || config?.AlertDefinitions == null || config.AlertDefinitions.Count == 0)
                return;

            // Загрузить предыдущее состояние аппарата
            var stateService = new BusinessLayer.Welding.Machine.MachineStateService(context);
            var latestState = stateService.GetCurrentWeldingMachineState(weldingMachineID);
            if (latestState != null && DateTime.Now.Subtract(latestState.DateCreated).TotalSeconds > 5)
            {
                // если с прошлого стейта прошло больше пяти секунд - игнорировать его
                latestState = null;
            }

            var checker = new BusinessLayer.Welding.Machine.ConditionsSetChecker();
            var sb = new StringBuilder();
            foreach (var alert in config.AlertDefinitions)
            {
                var result = checker.ValidateConditionsSet(state, alert);
                if (result)
                {
                    // проверить предыдущий стейт, был ли там - если был, то уведомление НЕ создавать
                    if (latestState != null && checker.ValidateConditionsSet(latestState, alert))
                    {
                        result = false;
                    }
                }

                // создать уведомление, если условие выполнилось и прошлый стейт или не валиден, или там не выполнялось условие
                if (result)
                {
                    sb.AppendLine(alert.Message);
                }
            }


            // Send notification?
            if (sb.Length > 0)
            {
                // Retrieve machine label
                var weldingMachineLabel = "N/A";
                int? organizationID = null;

                if (WeldingService.Domain.DomainObjects.WeldingMachines.ContainsKey(weldingMachineID))
                {
                    var machine = WeldingService.Domain.DomainObjects.WeldingMachines[weldingMachineID];

                    if (String.IsNullOrEmpty(machine.Label))
                        weldingMachineLabel = String.Format("{0} ({1})",
                            machine.Name,
                            machine.MAC);
                    else
                        weldingMachineLabel = String.Format("{0} ({1}, {2})",
                            machine.Name,
                            machine.Label,
                            machine.MAC);

                    try
                    {
                        organizationID = WeldingService.Domain.DomainObjects.OrganizationUnits[machine.OrganizationUnitID].OrganizationID;
                    }
                    catch { }
                }

                // Create notification
                var notificationParameters = new BusinessLayer.Models.Notifications.NotificationTypeWeldingMachineAlert.NotificationWeldingMachineAlertParameters
                {
                    WeldingMachineID = weldingMachineID,
                    WeldingMachineLabel = weldingMachineLabel,
                    AlertDatetime = DateTime.Now,
                    Message = state.FormatString(sb.ToString())
                };

                var notification = new BusinessLayer.Models.Notifications.NotificationTypeWeldingMachineAlert(notificationParameters);

                // Save notification
                var notificationService = new BusinessLayer.Services.Notifications.NotificationsService(context);
                notificationService.SaveByUserPermission(notification, "NotificationWeldingMachineAlert", organizationID);
            }
        }


        /// <summary>
        /// Проверка выходов за пределы в пассивном режиме
        /// </summary>
        void checkPassiveLimitsAndCreateNotifications(
            DataLayer.Welding.WeldingContext context,
            int weldingMachineID,
            BusinessLayer.Models.WeldingMachine.StateSummary state,
            Dictionary<string, BusinessLayer.Models.WeldingMachine.ProgramControlItemValue> dictProgramValues,
            WeldingMachineTypeConfiguration config
            )
        {
            var alerts = new List<BusinessLayer.Models.Notifications.NotificationTypeWeldingParametersAlert.ParameterAlert>();

            // Check Ranges (range_min, range_max)
            var rangesProperyCodes = config.Outbound.Body
                .Where(p => p.PropertyType == "range_min" && !String.IsNullOrEmpty(p.RangeSource))
                .Select(p => p.RangeSource);

            foreach (var propertyCode in rangesProperyCodes)
            {
                // find limits
                var program = dictProgramValues.Values.FirstOrDefault(pv => pv.ID == propertyCode);
                if (program != null && !(program.MinValue == 0 && program.MaxValue == 0))
                {
                    // got limits.

                    // check state
                    if (state.ContainsPropertyCode(propertyCode))
                    {
                        var value = state.GetNumericValue(propertyCode);

                        if (value < program.MinValue || value > program.MaxValue)
                        {
                            // try to lookup for Unit Code
                            var param = config.Inbound.Body.FirstOrDefault(p => p.PropertyCode == propertyCode);
                            var unitCode = param != null && param.Unit != null ? param.Unit.UnitCode : "";

                            // Property description
                            var propertyDescription = param != null ? param.Description : "";

                            // Create Alert!
                            var alert = new BusinessLayer.Models.Notifications.NotificationTypeWeldingParametersAlert.ParameterAlert
                            {
                                PropertyCode = propertyCode,
                                PropertyDescription = propertyDescription,
                                ActualValue = value.ToString("F"),
                                LimitValue = String.Format("{0} - {1}", program.MinValue.ToString("F"), program.MaxValue.ToString("F")),
                                Unit = unitCode
                            };

                            alerts.Add(alert);

                            // Update Property in State
                            var prop = state.Properties.ContainsKey(propertyCode) ? state.Properties[propertyCode] : null;
                            if (prop != null)
                            {
                                prop.LimitsExceeded = true;
                                prop.LimitMin = program.MinValue.ToString();
                                prop.LimitMax = program.MaxValue.ToString();
                            }
                        }
                    }
                }
            }

            // Check left/right controls
            foreach (var p in config.Outbound.Body)
            {
                if (p.PropertyCode == PropertyCodes.CtrlParm || p.PropertyCode == PropertyCodes.CtrlParmL || p.PropertyCode == PropertyCodes.CtrlParmR)
                {
                    // find which parameter is controlled
                    var ctrlValue = dictProgramValues.ContainsKey(p.PropertyCode) ? dictProgramValues[p.PropertyCode].Value : "";

                    // 01, 02, 04 - look in enums
                    if (!String.IsNullOrEmpty(ctrlValue))
                    {

                        // E.g. controlledPropertyValue contains '1' or '2'
                        // Now find in 'Ctrl.parm' enums what it really controls, e.g. 'State.I'
                        string controlledPropertyCode = fetchEnumDescription(config.Outbound.Body, p.PropertyCode, ctrlValue);

                        if (!String.IsNullOrEmpty(controlledPropertyCode))
                        {
                            // has program?
                            var program = dictProgramValues.Values.FirstOrDefault(pv =>
                                pv.ID == controlledPropertyCode
                                && !(pv.MinValue == 0 && pv.MaxValue == 0)
                                );

                            // get value
                            if (program != null && state.ContainsPropertyCode(controlledPropertyCode))
                            {
                                var value = state.GetNumericValue(controlledPropertyCode);

                                if (value < program.MinValue || value > program.MaxValue)
                                {
                                    // try to lookup for Unit Code
                                    var param = config.Inbound.Body.FirstOrDefault(parm => parm.PropertyCode == controlledPropertyCode);
                                    var unitCode = param != null && param.Unit != null ? param.Unit.UnitCode : "";

                                    // Property description
                                    var propertyDescription = param != null ? param.Description : "";

                                    // Create Alert!
                                    var alert = new BusinessLayer.Models.Notifications.NotificationTypeWeldingParametersAlert.ParameterAlert
                                    {
                                        PropertyCode = controlledPropertyCode,
                                        PropertyDescription = propertyDescription,
                                        ActualValue = value.ToString("F"),
                                        LimitValue = String.Format("{0} - {1}", program.MinValue.ToString("F"), program.MaxValue.ToString("F")),
                                        Unit = unitCode
                                    };

                                    alerts.Add(alert);

                                    // Update Property in State
                                    var prop = state.Properties.ContainsKey(controlledPropertyCode) ? state.Properties[controlledPropertyCode] : null;
                                    if (prop != null)
                                    {
                                        prop.LimitsExceeded = true;
                                        prop.LimitMin = program.MinValue.ToString();
                                        prop.LimitMax = program.MaxValue.ToString();
                                    }
                                }
                            }
                        }

                    }

                }
            }

            // Any alerts?
            if (alerts.Count > 0)
            {
                // Set to State
                state.LimitsExceeded = true;

                // Retrieve machine label
                var weldingMachineLabel = "N/A";
                int? organizationID = null;

                if (WeldingService.Domain.DomainObjects.WeldingMachines.ContainsKey(weldingMachineID))
                {
                    var machine = WeldingService.Domain.DomainObjects.WeldingMachines[weldingMachineID];

                    if (String.IsNullOrEmpty(machine.Label))
                        weldingMachineLabel = String.Format("{0} ({1})",
                            machine.Name,
                            machine.MAC);
                    else
                        weldingMachineLabel = String.Format("{0} ({1}, {2})",
                            machine.Name,
                            machine.Label,
                            machine.MAC);

                    try
                    {
                        organizationID = WeldingService.Domain.DomainObjects.OrganizationUnits[machine.OrganizationUnitID].OrganizationID;
                    }
                    catch { }
                }

                // Create notification
                var notificationParameters = new BusinessLayer.Models.Notifications.NotificationTypeWeldingParametersAlert.NotificationWeldingParametersAlertParameters
                {
                    WeldingMachineID = weldingMachineID,
                    WeldingMachineLabel = weldingMachineLabel,
                    AlertDatetime = DateTime.Now,
                    ParameterAlerts = alerts
                };

                var notification = new BusinessLayer.Models.Notifications.NotificationTypeWeldingParametersAlert(notificationParameters);

                // Save notification
                var notificationService = new BusinessLayer.Services.Notifications.NotificationsService(context);
                notificationService.SaveByUserPermission(notification, "NotificationWeldingParametersLimit", organizationID);
            }
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



        void dump(Models.Packet packet)
        {
            try
            {
                using (var context = GetWeldingContext())
                {
                    var dump = new DataLayer.Welding.Dump
                    {
                        DateCreated = DateTime.Now,
                        IP = packet.IP,
                        MAC = packet.MAC,
                        Data = packet.Data
                    };

                    context.Dumps.Add(dump);

                    try
                    {
                        context.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "Error Dumping message 2");
                    }

                    // Detach message, so they won't be collected in memory
                    context.Entry(dump).State = System.Data.Entity.EntityState.Detached;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error Dumping message 1");
            }
        }
    }
}
