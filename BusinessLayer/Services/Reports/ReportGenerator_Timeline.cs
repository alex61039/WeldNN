using BusinessLayer.Models;
using BusinessLayer.Models.Configuration;
using BusinessLayer.Services.Storage;
using BusinessLayer.Welding.Configuration;
using BusinessLayer.Welding.Machine;
using BusinessLayer.Welding.Panel;
using DataLayer.Welding;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.Entity;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using BusinessLayer.Interfaces.Reports;
using OfficeOpenXml.Drawing.Chart;
using BusinessLayer.Interfaces.Context;

namespace BusinessLayer.Services.Reports
{
    public class ReportGenerator_Timeline : IReportGenerator
    {
        IWeldingContextFactory _weldingContextFactory;
        // WeldingContext _context;
        FlowsCalculator flowsCalculator;


        public ReportGenerator_Timeline(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;

            flowsCalculator = new FlowsCalculator(weldingContextFactory);
        }

        public class TimelineReport_Result
        {
            public DateTime Date { get; set; }
            public TimeSpan TimeFrom { get; set; }
            public TimeSpan TimeTo { get; set; }

            // Dictionary by Welding Machines
            public IDictionary<int, TimelineReport_WeldingMachineItem> Items { get; set; }

            public string TimeFromText
            {
                get
                {
                    return String.Format("{0}:{1}:{2}", (int)TimeFrom.TotalHours, TimeFrom.ToString("mm"), TimeFrom.ToString("ss"));
                }
            }

            public string TimeToText
            {
                get
                {
                    return String.Format("{0}:{1}:{2}", (int)TimeTo.TotalHours, TimeTo.ToString("mm"), TimeTo.ToString("ss"));
                }
            }

            public string DateText
            {
                get
                {
                    return this.Date.ToString("yyyy-MM-dd");
                }
            }


            public TimelineReport_Result()
            {
                Date = DateTime.Now.Date;
                TimeFrom = TimeSpan.FromSeconds(0);
                TimeTo = TimeSpan.Parse("23:59:59");
                // TimeTo = TimeSpan.FromHours(24);
                Items = new Dictionary<int, TimelineReport_WeldingMachineItem>();
            }
        }

        public class TimelineReport_WeldingMachineItem
        {
            public DateTime Date { get; set; }

            public int WeldingMachineID { get; set; }
            public string WeldingMachineName { get; set; }
            public string WeldingMachineMac { get; set; }

            public int WeldingMachineTypeID { get; set; }
            public string WeldingMachineTypeName { get; set; }

            public int OrganizaionUnitID { get; set; }
            public String OrganizationUnitName { get; set; }


            public double TotalTimeMs { get; set; }
            public double WorkTimeMs { get; set; }
            public double StandbyTimeMs { get; set; }
            public double ErrorTimeMs { get; set; }

            public double Wire { get; set; }
            public double Gas { get; set; }  // литры
            public double Electricity { get; set; }


            public ICollection<TimelineReport_TimeItem> Standby_Items { get; set; }
            public ICollection<TimelineReport_TimeItem> Working_Items { get; set; }
            public ICollection<TimelineReport_TimeItem> Limits_Items { get; set; }

            public string DateText
            {
                get
                {
                    return this.Date.ToString("yyyy-MM-dd");
                }
            }
        }

        public class TimelineReport_TimeItem
        {
            public TimeSpan TimeFrom { get; set; }
            public TimeSpan TimeTo { get; set; }

            public string TimeFromText
            {
                get
                {
                    return String.Format("{0}:{1}:{2}", (int)TimeFrom.TotalHours, TimeFrom.ToString("mm"), TimeFrom.ToString("ss"));
                }
            }

            public string TimeToText
            {
                get
                {
                    return String.Format("{0}:{1}:{2}", (int)TimeTo.TotalHours, TimeTo.ToString("mm"), TimeTo.ToString("ss"));
                }
            }
        }



        struct GeneralReportItem
        {
            public int OrganizaionUnitID;
            public String OrganizationUnitName;
            public DateTime? Date;
            public int? UserAccountID;
            public string UserAccountName;

            public int WeldingMachineID;
            public string WeldingMachineName;

            public string WeldingMachineMac;
            public int WeldingMachineTypeID;
            public string WeldingMachineTypeName;

            public double TotalTimeMs;
            public double WorkTimeMs;
            public double StandbyTimeMs;
            public double ErrorTimeMs;

            public double Wire;
            public double Gas;  // литры
            public double Electricity;

            public string Key()
            {
                return string.Format("{0}_{1}_{2}_{3}",
                    // Date.ToString("yyyyMMdd_HHmm"), 
                    Date.HasValue ? Date.Value.ToString("yyyyMMdd") : "",
                    OrganizaionUnitID,
                    UserAccountID.HasValue ? UserAccountID.Value : 0,
                    WeldingMachineID
                    );
            }
        }

        private TimelineReport_Result PrepareData_GeneralReport(ReportRequest req)
        {
            var result = new TimelineReport_Result
            {
                Date = req.Date.Value.Date
            };

            // Dictionary of configs
            var configs = new Dictionary<int, WeldingMachineTypeConfiguration>();



            // Time
            TimeSpan? timeFrom = !String.IsNullOrEmpty(req.TimeFrom) ? TimeSpan.Parse(req.TimeFrom) : (TimeSpan?)null;
            TimeSpan? timeTo = !String.IsNullOrEmpty(req.TimeTo) ? TimeSpan.Parse(req.TimeTo) : (TimeSpan?)null;
            if (timeFrom != null)
                result.TimeFrom = timeFrom.Value;
            if (timeTo != null)
                result.TimeTo = timeTo.Value;

            using (var __context = _weldingContextFactory.CreateContext(18000))
            {
                var data = __context.Report_General(
                    req.Date,
                    req.Date,
                    req.UserAccountID.GetValueOrDefault() == 0 ? (int?)null : req.UserAccountID,
                    req.OrganizationUnitID.GetValueOrDefault() == 0 ? (int?)null : req.OrganizationUnitID,
                    req.WeldingMachineID.GetValueOrDefault() == 0 ? (int?)null : req.WeldingMachineID
                    );

                foreach (var d in data)
                {
                    // Check OrganizationUnitID
                    if (!req.OrganizationUnitIDs.Contains(d.OrganizationUnitID.Value))
                        continue;

                    // Check time
                    bool skipRow = false;
                    if (timeFrom.HasValue && d.DateCreated.TimeOfDay < timeFrom.Value)
                        skipRow = true;
                    if (timeTo.HasValue && d.DateCreated.TimeOfDay > timeTo.Value)
                        skipRow = true;


                    if (skipRow)
                        continue;


                    WeldingMachineStatus status = (WeldingMachineStatus)d.WeldingMachineStatus;

                    // КПД аппарата и мощность при простое - из конфига
                    var machine = getWeldingMachine(d.WeldingMachineID);
                    WeldingMachineTypeConfiguration config = null;

                    if (!configs.ContainsKey(d.WeldingMachineID))
                    {
                        using (var _context = _weldingContextFactory.CreateContext(0))
                        {
                            WeldingMachineTypeConfigurationLoader confLoader = new WeldingMachineTypeConfigurationLoader(_context);
                            config = confLoader.LoadByMachine(d.WeldingMachineID);
                        }

                        configs.Add(d.WeldingMachineID, config);
                    }
                    else
                    {
                        config = configs[d.WeldingMachineID];
                    }


                    TimelineReport_WeldingMachineItem item = null;
                    if (result.Items.ContainsKey(machine.ID))
                    {
                        item = result.Items[machine.ID];
                    }
                    else
                    {
                        item = new TimelineReport_WeldingMachineItem
                        {
                            Date = d.DateCreated.Date,

                            WeldingMachineID = d.WeldingMachineID,
                            WeldingMachineName = machine != null ? machine.Name : "",
                            WeldingMachineMac = d.WeldingMachineMAC,

                            WeldingMachineTypeID = machine != null ? machine.WeldingMachineTypeID : 0,
                            WeldingMachineTypeName = machine != null && machine.WeldingMachineType != null ? machine.WeldingMachineType.Name : "",

                            OrganizaionUnitID = d.OrganizationUnitID.Value,
                            OrganizationUnitName = d.OrganizationUnitName,

                            TotalTimeMs = 0,
                            WorkTimeMs = 0,
                            StandbyTimeMs = 0,
                            ErrorTimeMs = 0,

                            Wire = 0,
                            Gas = 0,
                            Electricity = 0,

                            Standby_Items = new List<TimelineReport_TimeItem>(),
                            Working_Items = new List<TimelineReport_TimeItem>(),
                            Limits_Items = new List<TimelineReport_TimeItem>()
                        };

                        result.Items[machine.ID] = item;
                    }



                    // ВРЕМЯ
                    item.TotalTimeMs += d.StateDurationMs;
                    item.WorkTimeMs += status == WeldingMachineStatus.Working ? d.StateDurationMs : 0;
                    item.StandbyTimeMs += status == WeldingMachineStatus.Ready ? d.StateDurationMs : 0;
                    item.ErrorTimeMs += (status == WeldingMachineStatus.Error || !String.IsNullOrEmpty(d.ErrorCode)) ? d.StateDurationMs : 0;

                    // РАСХОДЫ
                    // Расход электричества
                    item.Electricity += flowsCalculator.CalculateElectricity(config, d);

                    // Wire/Расход прволоки (кг/мин)
                    // только в режиме сварки
                    item.Wire += flowsCalculator.CalculateWireFlow(d);

                    // Gas/Расход газа (литры)
                    // только в режиме сварки
                    item.Gas += flowsCalculator.CalculateGasFlow(d);

                    // Продлить TimeItem или создать новый
                    TimelineReport_TimeItem timeItem = new TimelineReport_TimeItem
                    {
                        TimeFrom = d.DateCreated.TimeOfDay,
                        TimeTo = d.DateCreated.TimeOfDay.Add(TimeSpan.FromMilliseconds(d.StateDurationMs))
                    };

                    if (status == WeldingMachineStatus.Ready)
                    {
                        addTimeItem(item.Standby_Items, timeItem);
                    }
                    if (status == WeldingMachineStatus.Working)
                    {
                        addTimeItem(item.Working_Items, timeItem);
                    }
                    if (status == WeldingMachineStatus.Working && d.ControlState == "Limited")
                    {
                        addTimeItem(item.Limits_Items, timeItem);
                    }


                    // Exists?
                    /*
                    var key = item.Key();
                    if (!dict.ContainsKey(key))
                    {
                        // Add new item
                        dict.Add(key, item);
                    }
                    else
                    {
                        // Update existing item
                        var existing_item = dict[key];

                        existing_item.TotalTimeMs += item.TotalTimeMs;
                        existing_item.WorkTimeMs += item.WorkTimeMs;
                        existing_item.StandbyTimeMs += item.StandbyTimeMs;
                        existing_item.ErrorTimeMs += item.ErrorTimeMs;
                        existing_item.Wire += item.Wire;
                        existing_item.Gas += item.Gas;
                        existing_item.Electricity += item.Electricity;

                        dict[key] = existing_item;
                    }
                    */
                }
            }

            // Add zero-row if no data
            /*
            if (dict == null || dict.Count == 0)
            {
                dict = new SortedDictionary<string, GeneralReportItem>();

                // add empty rows with welding machines
                var machines = _context.WeldingMachines.Where(m => m.Status == (int)GeneralStatus.Active);

                if (req.WeldingMachineID.HasValue && req.WeldingMachineID.Value > 0)
                    machines = machines.Where(m => m.ID == req.WeldingMachineID.Value);
                else if (req.OrganizationUnitID.HasValue && req.OrganizationUnitID.Value > 0)
                    machines = machines.Where(m => m.OrganizationUnitID == req.OrganizationUnitID.Value);

                machines = machines
                    .Include(m => m.OrganizationUnit)
                    .OrderBy(m => m.Name);


                foreach (var m in machines)
                {
                    dict.Add(m.ID.ToString(), new GeneralReportItem
                    {
                        OrganizaionUnitID = m.OrganizationUnitID,
                        OrganizationUnitName = m.OrganizationUnit.Name,
                        Date = null,
                        WeldingMachineID = m.ID,
                        WeldingMachineName = m.Name
                    });
                }

                // Still no welding machines?
                if (result.Items.Count == 0)
                {
                    dict.Add("empty", new GeneralReportItem
                    {
                        OrganizaionUnitID = 0,
                        OrganizationUnitName = "",
                        Date = null,
                        UserAccountID = 0,
                        UserAccountName = "",
                        WeldingMachineID = 0,
                        WeldingMachineName = "-",

                        TotalTimeMs = 0,
                        WorkTimeMs = 0,
                        StandbyTimeMs = 0,
                        ErrorTimeMs = 0,

                        Wire = 0,
                        Gas = 0,
                        Electricity = 0
                    });
                }
            }
            */

            return result;
        }

        /// <summary>
        /// Добавляет к существующему интервалу, или создает новый интервал времени
        /// </summary>
        void addTimeItem(ICollection<TimelineReport_TimeItem> items, TimelineReport_TimeItem item)
        {
            var bFound = false;

            foreach(var i in items)
            {
                var diff = Math.Abs(item.TimeFrom.Subtract(i.TimeTo).TotalMilliseconds);
                if (diff <= 1500)
                {
                    // Remove from list
                    items.Remove(i);

                    // Prolongate
                    var newItem = new TimelineReport_TimeItem {
                        TimeFrom = i.TimeFrom < item.TimeFrom ? i.TimeFrom : item.TimeFrom, // min
                        TimeTo = i.TimeTo > item.TimeTo ? i.TimeTo : item.TimeTo // max
                    };

                    // Add to list
                    items.Add(newItem);

                    bFound = true;
                    break;
                }
            }

            // Not prolongated any - add as is
            if (!bFound)
            {
                items.Add(item);
            }
        }


        Dictionary<int, WeldingMachine> dictMachines;
        WeldingMachine getWeldingMachine(int WeldingMachineID)
        {
            if (dictMachines == null)
                dictMachines = new Dictionary<int, WeldingMachine>();

            if (dictMachines.ContainsKey(WeldingMachineID))
                return dictMachines[WeldingMachineID];

            // Load from db
            using (var _context = _weldingContextFactory.CreateContext(0))
            {
                var program = _context.WeldingMachines.Include(m => m.WeldingMachineType).FirstOrDefault(m => m.ID == WeldingMachineID);
                if (program == null)
                    return null;

                dictMachines[WeldingMachineID] = program;

                return program;
            }
        }

        public ReportGeneratorResult Generate(ReportRequest req)
        {
            var result = PrepareData_GeneralReport(req);

            return new ReportGeneratorResult
            {
                ExcelData = null,
                JSON = result != null ? Newtonsoft.Json.JsonConvert.SerializeObject(result) : null
            };
        }

    }
}
