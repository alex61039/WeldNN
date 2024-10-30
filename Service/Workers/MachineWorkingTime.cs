using BusinessLayer.Models;
using BusinessLayer.Models.Configuration;
using BusinessLayer.Services.Notifications;
using BusinessLayer.Welding.Configuration;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Workers
{
    public class MachineWorkingTime : ServiceHelpers.PeriodicWorker
    {
        public MachineWorkingTime(int periodSeconds, bool skipFirstRun) : base(periodSeconds, skipFirstRun)
        {
            Action = InternalCheck;

            // Run on initialization
            // InternalCheck();
        }

        protected override bool UseAbort
        {
            get { return true; }
        }

        protected void InternalCheck()
        {
            Logger.Log(LogLevel.Debug, "MachineWorkingTime - STARTING");

            // Load machines and configs
            List<WeldingMachine> machines = new List<WeldingMachine>();
            Dictionary<int, WeldingMachineTypeConfiguration> configs = new Dictionary<int, WeldingMachineTypeConfiguration>();
            Dictionary<int, bool> onMaintenances = new Dictionary<int, bool>();
            using (var context = GetWeldingContext())
            {
                // all active Welding Machines
                machines = context.WeldingMachines.Where(m => m.Status == (int)GeneralStatus.Active).ToList();

                // Загрузить конфиги
                var configLoader = new WeldingMachineTypeConfigurationLoader(context);
                foreach(var m in machines)
                {
                    // Конфиг
                    configs.Add(m.ID, configLoader.LoadByMachine(m.ID));

                    // check if machine is on Service
                    var onService = context.Maintenances.Any(s => s.WeldingMachineID == m.ID
                        && s.Status == (int)GeneralStatus.Active
                        && s.MaintenanceStatus == (int)MaintenanceStatus.InProcess);

                    onMaintenances.Add(m.ID, onService);
                }
            }

            using (var context = GetWeldingContext())
            using (var transaction = context.Database.BeginTransaction(System.Data.IsolationLevel.ReadUncommitted))
            {
                foreach (var m in machines)
                {
                    var config = configs[m.ID];
                    if (config == null)
                        continue;


                    var onService = onMaintenances[m.ID];
                    if (onService)
                        continue;

                    long TimeTotalSecs = 0;
                    long TimeAfterLastServiceSecs = 0;
                    long TimeTillNextServiceSecs = 0;

                    // Total time
                    try
                    {
                        var result = context.EdmGetMachineWorkingTime(m.ID, null, null);
                        TimeTotalSecs = result.First().Value;
                    }
                    catch { }

                    // Time since last service
                    try
                    {
                        var result = context.EdmGetMachineWorkingTimeSinceLastService(m.ID, null);
                        TimeAfterLastServiceSecs = result.First().Value;
                    }
                    catch { }

                    // Time till next service
                    try
                    {
                        if (config != null && config.Settings != null && config.Settings.WorkingTimeBeforeService > 0)
                        {
                            var ts = TimeSpan.FromHours(config.Settings.WorkingTimeBeforeService);
                            ts = ts.Subtract(TimeSpan.FromSeconds(TimeAfterLastServiceSecs));

                            TimeTillNextServiceSecs = (long)ts.TotalSeconds;
                        }
                    }
                    catch { }

                    // Send notification?
                    CheckSendMaintenanceNotificaion_ByHours(m, TimeAfterLastServiceSecs, config);
                    CheckSendMaintenanceNotificaion_ByDays(m, config);

                    // Save changes
                    try
                    {
                        using (var context2 = GetWeldingContext())
                        {
                            var machine = context2.WeldingMachines.Find(m.ID);

                            machine.TimeTotalSecs = TimeTotalSecs;
                            machine.TimeAfterLastServiceSecs = TimeAfterLastServiceSecs;
                            machine.TimeTillNextServiceSecs = TimeTillNextServiceSecs;

                            context2.SaveChanges();
                        }
                    }
                    catch (Exception ex) {
                        Logger.LogException(ex, "MachineWorkingTime");
                    }

                }
            }

            Logger.Log(LogLevel.Debug, "MachineWorkingTime - FINISHED");

        }

        protected void CheckSendMaintenanceNotificaion_ByHours(
            WeldingMachine machine,
            long TimeAfterLastServiceSecs,
            WeldingMachineTypeConfiguration config
            )
        {
            if (config == null)
                return;

            // Load config
            using (var context = GetWeldingContext())
            {
                // Уже было уведомление?
                int? UserServiceNotifiedBeforeHours = machine.UserServiceNotifiedBeforeHours;

                // Междусервисный интервал
                long timeBetweenMaintenanceSecs = (config.Settings != null ? config.Settings.WorkingTimeBeforeService : 0) * 3600;
                if (timeBetweenMaintenanceSecs <= 0)
                    return;

                // Определить следующее время отправки уведомлений
                if (config.Settings == null || config.Settings.NotifyHoursBeforeService == null || config.Settings.NotifyHoursBeforeService.Count == 0)
                    return;

                int Next_NotifyHoursBeforeService_Secs = 0;
                try
                {
                    // Выбрать максимальное значение из тех, что меньше уже отправленного ранее
                    Next_NotifyHoursBeforeService_Secs = config.Settings.NotifyHoursBeforeService
                        .Where(h => !UserServiceNotifiedBeforeHours.HasValue || h < UserServiceNotifiedBeforeHours.Value)
                        .Max()
                        * 3600;
                }
                catch { }

                if (Next_NotifyHoursBeforeService_Secs <= 0)
                    return;


                var machineTimeAfterServiceSecs = (int)(machine.TimeAfterLastServiceSecs.GetValueOrDefault());
                if (timeBetweenMaintenanceSecs - machineTimeAfterServiceSecs < Next_NotifyHoursBeforeService_Secs)
                {
                    int? organizaionID = null;
                    try
                    {
                        organizaionID = context.OrganizationUnits.Find(machine.OrganizationUnitID).OrganizationID;
                    }
                    catch { }

                    var notification = new BusinessLayer.Models.Notifications.NotificationTypeMaintenance(
                        machine.ID,
                        machine.Name,
                        machine.MAC,
                        (int)(Next_NotifyHoursBeforeService_Secs / 3600),
                        0
                        );

                    var notificationService = new NotificationsService(context);
                    notificationService.SaveByUserPermission(notification, "NotificationMaintenance", organizaionID);


                    // Апдейтнуть в базе
                    var machine2 = context.WeldingMachines.Find(machine.ID);
                    machine2.UserServiceNotifiedOn = DateTime.Now;
                    machine2.UserServiceNotifiedBeforeHours = (int)(Next_NotifyHoursBeforeService_Secs / 3600);
                    context.SaveChanges();
                }
            }
        }

        protected void CheckSendMaintenanceNotificaion_ByDays(
            WeldingMachine machine,
            WeldingMachineTypeConfiguration config
            )
        {
            if (config == null)
                return;

            // Load config
            using (var context = GetWeldingContext())
            {
                // Уже было уведомление сегодня?
                if (machine.UserServiceNotifiedOn.HasValue)
                {
                    if (machine.UserServiceNotifiedOn.Value.Date == DateTime.Now.Date)
                        return;
                }

                // В конфиге есть параметр?
                if (config.Settings == null || config.Settings.NotifyDaysSinceService <= 0)
                    return;

                // Дата последнего обслуживания/начало эксплуатации
                DateTime? dateSince = machine.LastServiceOn;

                if (!dateSince.HasValue)
                {
                    dateSince = machine.DateStartedUsing;
                }

                if (!dateSince.HasValue)
                {
                    dateSince = machine.DateCreated;
                }


                // Calculate total days
                double totalDays = DateTime.Now.Subtract(dateSince.Value).TotalDays;

                // Create notification
                if ((int)totalDays == config.Settings.NotifyDaysSinceService)
                {
                    int? organizaionID = null;
                    try
                    {
                        organizaionID = context.OrganizationUnits.Find(machine.OrganizationUnitID).OrganizationID;
                    }
                    catch { }

                    var notification = new BusinessLayer.Models.Notifications.NotificationTypeMaintenance(
                        machine.ID,
                        machine.Name,
                        machine.MAC,
                        0,
                        config.Settings.NotifyDaysSinceService
                        );

                    var notificationService = new NotificationsService(context);
                    notificationService.SaveByUserPermission(notification, "NotificationMaintenance", organizaionID);


                    // Апдейтнуть в базе
                    var machine2 = context.WeldingMachines.Find(machine.ID);
                    machine2.UserServiceNotifiedOn = DateTime.Now;
                    context.SaveChanges();
                }
            }

        }
    }
}