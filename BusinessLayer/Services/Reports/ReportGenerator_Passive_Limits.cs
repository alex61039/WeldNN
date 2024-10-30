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
using OfficeOpenXml.Style;
using BusinessLayer.Interfaces.Context;

namespace BusinessLayer.Services.Reports
{
    public class ReportGenerator_Passive_Limits : IReportGenerator
    {
        // WeldingContext _context;
        IWeldingContextFactory _weldingContextFactory;

        public ReportGenerator_Passive_Limits(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
        }


        struct ExceededProperty
        {
            public string PropertyCode;
            public string PropertyDescription;
            public double Min;
            public double Max;
            public double Value;
        }

        class ReportItem
        {
            public DateTime Date;

            public int OrganizaionUnitID;
            public String OrganizationUnitName;
            public int? UserAccountID;
            public string UserAccountName;

            public int WeldingMachineID;
            public string WeldingMachineName;
            public string WeldingMachineMac;
            public int WeldingMachineTypeID;
            public string WeldingMachineTypeName;

            public int DurationMs;

            public SortedDictionary<string, ExceededProperty> ExceededProperties;

            public string PropertiesHash()
            {
                if (ExceededProperties == null || ExceededProperties.Count == 0)
                    return "";

                var sb = new StringBuilder();
                foreach(var kv in ExceededProperties)
                {
                    sb.AppendFormat("{0}_{1}_", kv.Value.PropertyCode, kv.Value.Value);
                }

                return sb.ToString();
            }

            public string Key()
            {
                return string.Format("{0}_{1}_{2}_{3}_{4}",
                    Date.ToString("yyyyMMdd_HHmmss"),
                    OrganizaionUnitID,
                    UserAccountID.HasValue ? UserAccountID.Value : 0,
                    WeldingMachineID,
                    PropertiesHash()
                    );
            }
        }

        private ReportItem CreateReportItem(WeldingMachineState d)
        {
            // worker user
            var user = getUserAccount(d.RFID);
            int? userAccountID = user == null ? (int?)null : user.ID;

            var item = new ReportItem
            {
                Date = d.DateCreated,

                OrganizaionUnitID = d.WeldingMachine.OrganizationUnitID,
                OrganizationUnitName = d.WeldingMachine.OrganizationUnit.Name,
                UserAccountID = user == null ? (int?)null : user.ID,
                UserAccountName = user == null ? "" : user.Name,

                WeldingMachineID = d.WeldingMachineID,
                WeldingMachineName = d.WeldingMachine.Name,
                WeldingMachineMac = d.WeldingMachine.MAC,
                WeldingMachineTypeID = d.WeldingMachine.WeldingMachineTypeID,
                WeldingMachineTypeName = d.WeldingMachine.WeldingMachineType.Name,

                DurationMs = d.StateDurationMs
            };

            // Machine config
            var config = getConfig(item.WeldingMachineTypeID);

            // Exceeded properites
            item.ExceededProperties = new SortedDictionary<string, ExceededProperty>();
            foreach(var p in d.WeldingMachineParameterValues.Where(p => p.LimitsExceeded == true))
            {
                // Fetch description
                var PropertyDescription = "";
                if (config != null)
                {
                    PropertyDescription = config.GetPropertyDescription(p.PropertyCode);
                }

                var prop = new ExceededProperty
                {
                    PropertyCode = p.PropertyCode,
                    PropertyDescription = PropertyDescription,
                    Min = Double.TryParse(p.LimitMin, out double tmp) ? tmp : 0.0,
                    Max = Double.TryParse(p.LimitMax, out double tmp2) ? tmp2 : 0.0,
                    Value = Double.TryParse(p.Value, out double tmp3) ? tmp3 : 0.0
                };

                if (!item.ExceededProperties.ContainsKey(p.PropertyCode))
                    item.ExceededProperties.Add(p.PropertyCode, prop);
            }

            return item;
        }

        private SortedDictionary<string, ReportItem> prepareReportData(ReportRequest req)
        {
            var dict = new SortedDictionary<string, ReportItem>();

            // DATA
            using (var __context = _weldingContextFactory.CreateContext(18000))
            {
                var query = __context.WeldingMachineStates
                    .Where(s => s.Control == "Passive" && s.LimitsExceeded == true)
                    .Include(s => s.WeldingMachine)
                    .Include(s => s.WeldingMachine.WeldingMachineType)
                    .Include(s => s.WeldingMachine.OrganizationUnit)
                    .Include(s => s.WeldingMachineParameterValues)
                    .Where(s => req.OrganizationUnitIDs.Contains(s.WeldingMachine.OrganizationUnitID));


                // FILTERS

                // date from
                if (req.DateFrom.HasValue)
                    query = query.Where(s => s.DateCreated >= req.DateFrom.Value);

                // date to
                if (req.DateTo.HasValue)
                {
                    var next_day = req.DateTo.Value.Date.AddDays(1);
                    query = query.Where(s => s.DateCreated < next_day);
                }


                // worker user
                if (req.UserAccountID.HasValue && req.UserAccountID.Value > 0)
                {
                    var user = __context.UserAccounts.FirstOrDefault(u => u.ID == req.UserAccountID.Value);
                    if (user != null && !String.IsNullOrEmpty(user.RFID_Hex))
                    {
                        query = query.Where(s => s.RFID == user.RFID_Hex);
                    }
                }

                // organization unit
                if (req.OrganizationUnitID.HasValue && req.OrganizationUnitID.Value > 0)
                {
                    query = query.Where(s => s.WeldingMachine.OrganizationUnitID == req.OrganizationUnitID.Value);
                }

                //Machine
                if (req.WeldingMachineID.HasValue && req.WeldingMachineID.Value > 0)
                {
                    query = query.Where(s => s.WeldingMachineID == req.WeldingMachineID.Value);
                }

                // SORT
                query = query.OrderBy(s => s.DateCreated);


                TimeSpan? time_from = String.IsNullOrEmpty(req.TimeFrom) ? (TimeSpan?)null : TimeSpan.Parse(req.TimeFrom);
                TimeSpan? time_to = String.IsNullOrEmpty(req.TimeTo) ? (TimeSpan?)null : TimeSpan.Parse(req.TimeTo);

                foreach (var d in query)
                {
                    // Compare time
                    if (time_from.HasValue && d.DateCreated.TimeOfDay < time_from.Value)
                        continue;
                    if (time_to.HasValue && d.DateCreated.TimeOfDay > time_to.Value)
                        continue;

                    // Create report item
                    var item = CreateReportItem(d);

                    // Look for near item
                    bool item_prolongated = false;
                    try
                    {
                        // Найти ближайшую предыдущую строку репорта по этому же аппарату и сравнить параметры
                        var nearest_item = dict.Where(s =>
                                s.Value.WeldingMachineID == d.WeldingMachineID
                                && s.Value.Date < d.DateCreated
                            )
                            .OrderByDescending(kv => kv.Value.Date)
                            .Take(1)
                            .FirstOrDefault();

                        // Сравнить параметры и сравнить разницу во времени
                        if (nearest_item.Key != null && item.PropertiesHash() == nearest_item.Value.PropertiesHash())
                        {
                            var item_date_end = nearest_item.Value.Date + TimeSpan.FromMilliseconds(nearest_item.Value.DurationMs);
                            var d_date_end = d.DateCreated + TimeSpan.FromMilliseconds(d.StateDurationMs);

                            // nearest_item => d
                            var difference_secs = Math.Abs(d.DateCreated.Subtract(item_date_end).TotalSeconds);

                            // Prolongate existing item
                            if (difference_secs < 2)
                            {
                                item = nearest_item.Value;

                                // take maximum
                                var latest_end = (item_date_end > d_date_end) ? item_date_end : d_date_end;

                                // Set new duration, prolongate
                                item.DurationMs = (int)latest_end.Subtract(item.Date).TotalMilliseconds;

                                dict[nearest_item.Key] = item;

                                item_prolongated = true;
                            }
                        }
                    }
                    catch { }

                    if (!item_prolongated)
                    {
                        // Exists?
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

                            existing_item.DurationMs += item.DurationMs;

                            dict[key] = existing_item;
                        }
                    }


                }
            }

            // Add zero-row if no data
            if (dict == null || dict.Count == 0)
            {
                //dict.Add("empty", new ReportItem
                //{
                //});
            }

            return dict;
        }

        Dictionary<int, WeldingMachineTypeConfiguration> dictConfigs;
        WeldingMachineTypeConfiguration getConfig(int WeldingMachineTypeID)
        {
            if (dictConfigs == null)
                dictConfigs = new Dictionary<int, WeldingMachineTypeConfiguration>();

            if (dictConfigs.ContainsKey(WeldingMachineTypeID))
                return dictConfigs[WeldingMachineTypeID];

            // Load config
            using (var __context = _weldingContextFactory.CreateContext(0))
            {
                var configLoader = new WeldingMachineTypeConfigurationLoader(__context);
                var config = configLoader.LoadByType(WeldingMachineTypeID);
                if (config == null)
                    return null;

                dictConfigs[WeldingMachineTypeID] = config;

                return config;
            }
        }

        Dictionary<string, UserAccount> dictUsers;
        UserAccount getUserAccount(string rfid_hex)
        {
            if (String.IsNullOrEmpty(rfid_hex) || rfid_hex == "000000")
                return null;

            if (dictUsers == null)
                dictUsers = new Dictionary<string, UserAccount>();

            if (dictUsers.ContainsKey(rfid_hex))
                return dictUsers[rfid_hex];

            // Load from db
            using (var __context = _weldingContextFactory.CreateContext(0))
            {
                var user = __context.UserAccounts.FirstOrDefault(u => u.RFID_Hex == rfid_hex);
                if (user == null)
                    return null;

                dictUsers[rfid_hex] = user;

                return user;
            }
        }

        public ReportGeneratorResult Generate(ReportRequest req)
        {
            byte[] fileContents;

            using (var package = new ExcelPackage())
            {
                var worksheet = BuildWorksheet(package, req);

                // Finally when you're done, export it to byte array.
                fileContents = package.GetAsByteArray();
            }


            return new ReportGeneratorResult { ExcelData = fileContents };
        }

        public ExcelWorksheet BuildWorksheet(ExcelPackage package, ReportRequest req)
        {
            // Prepare data
            var dict = prepareReportData(req);

            var worksheet = package.Workbook.Worksheets.Add("Sheet1");

            // TITLE
            worksheet.Cells[1, 1, 1, 12].Merge = true;
            worksheet.Cells[1, 1].Value = "Отчет о выходе параметров за пределы в пассивном режиме";
            worksheet.Cells[1, 1].Style.Font.Size = 20;
            worksheet.Cells[1, 1].Style.Font.Bold = true;

            // Dates range
            var title = "";
            if (req.DateFrom.HasValue && req.DateTo.HasValue)
            {
                var dt_from = req.DateFrom.Value.Date;
                var dt_to = req.DateTo.Value.Date;
                title = dt_from == dt_to ? req.DateFrom.Value.ToString("dd-MM-yyyy")
                    : String.Format("{0} - {1}", req.DateFrom.Value.ToString("dd-MM-yyyy"), req.DateTo.Value.ToString("dd-MM-yyyy"));
            }
            else if (req.DateFrom.HasValue)
                title = String.Format("С {0}", req.DateFrom.Value.ToString("dd-MM-yyyy"));
            else if (req.DateTo.HasValue)
                title = String.Format("До {0}", req.DateTo.Value.ToString("dd-MM-yyyy"));

            worksheet.Cells[2, 1].Value = title;
            worksheet.Cells[2, 1].Style.Font.Bold = true;
            worksheet.Cells[2, 1].Style.Font.Size = 16;


            // Header
            int headerRow = 4;
            int row = headerRow;
            worksheet.Row(row).Style.Font.Bold = true;
            worksheet.Row(row).Style.WrapText = true;
            // worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Value = "#";
            worksheet.Cells[row, 2].Value = "Цех";
            worksheet.Cells[row, 3].Value = "Дата/время";
            worksheet.Cells[row, 4].Value = "MAC адрес";
            worksheet.Cells[row, 5].Value = "Аппарат";
            worksheet.Cells[row, 6].Value = "Тип аппарата";
            worksheet.Cells[row, 7].Value = "Сварщик";
            worksheet.Cells[row, 8].Value = "Длительность нарушения режима, ч:м:с";

            worksheet.Cells[row, 9].Value = "Параметр";
            worksheet.Cells[row, 10].Value = "Значение";
            worksheet.Cells[row, 11].Value = "Допуск минимум";
            worksheet.Cells[row, 12].Value = "Допуск максимум";

            worksheet.Column(1).Width = 5;
            worksheet.Column(2).Width = 30;
            worksheet.Column(2).Style.WrapText = true;
            worksheet.Column(3).Width = 20;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 25;
            worksheet.Column(6).Width = 20;
            worksheet.Column(7).Width = 30;

            worksheet.Column(8).Width = 25;
            worksheet.Column(8).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            worksheet.Column(9).Width = 25;
            worksheet.Column(9).Style.WrapText = true;

            worksheet.Column(10).Width = 20;
            worksheet.Column(10).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            worksheet.Column(11).Width = 20;
            worksheet.Column(11).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            worksheet.Column(12).Width = 20;
            worksheet.Column(12).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;


            // Data
            row++;
            int k = 1;
            TimeSpan ts;
            foreach (var item in dict.Values)
            {
                int z = 1;
                foreach (var p_kv in item.ExceededProperties)
                {
                    var p = p_kv.Value;

                    if (z == 1)
                    {
                        worksheet.Cells[row, 1].Value = k;
                        worksheet.Cells[row, 2].Value = item.OrganizationUnitName;
                        worksheet.Cells[row, 3].Value = item.Date.ToString("yyyy-MM-dd HH:mm:ss");
                        worksheet.Cells[row, 4].Value = item.WeldingMachineMac;
                        worksheet.Cells[row, 5].Value = item.WeldingMachineName;
                        worksheet.Cells[row, 6].Value = item.WeldingMachineTypeName;
                        worksheet.Cells[row, 7].Value = item.UserAccountName;

                        // Duration
                        ts = TimeSpan.FromMilliseconds(item.DurationMs);
                        worksheet.Cells[row, 8].Style.Numberformat.Format = "[h]:mm:ss";
                        worksheet.Cells[row, 8].Value = ts;
                    }

                    worksheet.Cells[row, 9].Value = String.Format("{0} ({1})", p.PropertyDescription, p.PropertyCode);
                    worksheet.Cells[row, 10].Value = p.Value;
                    worksheet.Cells[row, 11].Value = p.Min;
                    worksheet.Cells[row, 12].Value = p.Max;


                    row++;
                    z++;
                }

                k++;
            }

            // Total row
            if (dict.Count > 0)
            {
                worksheet.Row(row).Style.Font.Bold = true;

                worksheet.Cells[row, 7].Value = "Итого:";

                ts = TimeSpan.FromMilliseconds(dict.Values.Sum(item => item.DurationMs));
                worksheet.Cells[row, 8].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 8].Value = ts;

            }
            else
            {
                worksheet.Cells[row, 2].Value = "Нет данных за выбранный период";
            }

            return worksheet;
        }

    }
}
