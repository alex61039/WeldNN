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
    public class ReportGenerator_Errors : IReportGenerator
    {
        // WeldingContext _context;
        IWeldingContextFactory _weldingContextFactory;

        public ReportGenerator_Errors(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
        }

        struct ReportItem
        {
            public int OrganizaionUnitID;
            public String OrganizationUnitName;
            public DateTime Date;
            public int? UserAccountID;
            public string UserAccountName;
            public int WeldingMachineID;
            public string WeldingMachineName;
            public string WeldingMachineMac;
            public int WeldingMachineTypeID;
            public string WeldingMachineTypeName;

            public string ErrorCode;
            public int DurationMs;

            public string WeldingProgramName;

            public long WorkingTimeAfterServiceSecs;

            public string Key()
            {
                return string.Format("{0}_{1}_{2}_{3}_{4}",
                    // Date.ToString("yyyyMMdd_HHmm"), 
                    Date.ToString("yyyyMMdd_HHmmss"),
                    OrganizaionUnitID,
                    UserAccountID.HasValue ? UserAccountID.Value : 0,
                    WeldingMachineID,
                    ErrorCode
                    );
            }
        }

        private SortedDictionary<string, ReportItem> prepareReportData(ReportRequest req)
        {
            var dict = new SortedDictionary<string, ReportItem>();

            // DATA
            using (var __context = _weldingContextFactory.CreateContext(18000))
            {
                var query = __context.WeldingMachineStates
                    .Where(s => !String.IsNullOrEmpty(s.ErrorCode))
                    .Include(s => s.WeldingMachine)
                    .Include(s => s.WeldingMachine.WeldingMachineType)
                    .Include(s => s.WeldingMachine.OrganizationUnit)
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


                foreach (var d in query)
                {
                    // worker user
                    var user = getUserAccount(d.RFID);
                    int? userAccountID = user == null ? (int?)null : user.ID;

                    // program
                    var programname = d.WeldingLimitProgramName == "default" ? "По умолчанию" : d.WeldingLimitProgramName;

                    // Look for near item
                    bool item_prolongated = false;
                    try
                    {
                        var nearest_item = dict.Where(s =>
                                s.Value.WeldingMachineID == d.WeldingMachineID
                                && s.Value.ErrorCode == d.ErrorCode
                                && s.Value.UserAccountID == userAccountID
                                && s.Value.WeldingProgramName == programname
                                && s.Value.Date < d.DateCreated
                            )
                            .OrderByDescending(kv => kv.Value.Date)
                            .Take(1)
                            .FirstOrDefault();

                        if (nearest_item.Key != null)
                        {
                            var item_date_end = nearest_item.Value.Date + TimeSpan.FromMilliseconds(nearest_item.Value.DurationMs);
                            var d_date_end = d.DateCreated + TimeSpan.FromMilliseconds(d.StateDurationMs);

                            // nearest_item => d
                            var difference_secs = Math.Abs(d.DateCreated.Subtract(item_date_end).TotalSeconds);

                            // Prolongate existing item
                            if (difference_secs < 2)
                            {
                                var item = nearest_item.Value;

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
                        var item = new ReportItem
                        {
                            OrganizaionUnitID = d.WeldingMachine.OrganizationUnitID,
                            OrganizationUnitName = d.WeldingMachine.OrganizationUnit.Name,
                            Date = d.DateCreated,
                            UserAccountID = user == null ? (int?)null : user.ID,
                            UserAccountName = user == null ? "" : user.Name,

                            WeldingMachineID = d.WeldingMachineID,
                            WeldingMachineName = d.WeldingMachine.Name,
                            WeldingMachineMac = d.WeldingMachine.MAC,
                            WeldingMachineTypeID = d.WeldingMachine.WeldingMachineTypeID,
                            WeldingMachineTypeName = d.WeldingMachine.WeldingMachineType.Name,

                            ErrorCode = d.ErrorCode,
                            DurationMs = d.StateDurationMs,

                            WeldingProgramName = programname,

                            WorkingTimeAfterServiceSecs = 0

                        };

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

            // Process Working Time
            if (dict.Count > 0)
            {
                var dict2 = new SortedDictionary<string, ReportItem>();

                foreach (var kv in dict)
                {
                    try
                    {
                        using (var __context = _weldingContextFactory.CreateContext(9000))
                        {
                            var result = __context.EdmGetMachineWorkingTimeSinceLastService(kv.Value.WeldingMachineID, kv.Value.Date);
                            var worktimeSecs = result.First().Value;

                            var item = dict[kv.Key];
                            item.WorkingTimeAfterServiceSecs = worktimeSecs;

                            dict2.Add(kv.Key, item);
                        }
                    }
                    catch { }
                }

                dict = dict2;
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

        Dictionary<int, WeldingLimitProgram> dictPrograms;
        WeldingLimitProgram getProgram(int WeldingLimitProgramID)
        {
            if (dictPrograms == null)
                dictPrograms = new Dictionary<int, WeldingLimitProgram>();

            if (dictPrograms.ContainsKey(WeldingLimitProgramID))
                return dictPrograms[WeldingLimitProgramID];

            // Load from db
            using (var __context = _weldingContextFactory.CreateContext(0))
            {
                var program = __context.WeldingLimitPrograms.FirstOrDefault(m => m.ID == WeldingLimitProgramID);
                if (program == null)
                    return null;

                dictPrograms[WeldingLimitProgramID] = program;

                return program;
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
            worksheet.Cells[1, 1, 1, 5].Merge = true;
            worksheet.Cells[1, 1].Value = "Отчет ошибок оборудования";
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
            worksheet.Cells[row, 7].Value = "Номер ошибки";
            worksheet.Cells[row, 8].Value = "Длительность неисправного состояния, ч:м:с";
            worksheet.Cells[row, 9].Value = "Сварщик";
            worksheet.Cells[row, 10].Value = "Режим работы";
            worksheet.Cells[row, 11].Value = "Наработка после сервисного обслуживания, ч:м:с";

            worksheet.Column(1).Width = 5;
            worksheet.Column(2).Width = 30;
            worksheet.Column(2).Style.WrapText = true;
            worksheet.Column(3).Width = 20;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 25;
            worksheet.Column(6).Width = 20;
            worksheet.Column(7).Width = 20;
            worksheet.Column(7).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Column(8).Width = 15;
            worksheet.Column(9).Width = 25;
            worksheet.Column(10).Width = 20;
            worksheet.Column(11).Width = 15;

            // Data
            row++;
            int k = 1;
            TimeSpan ts;
            foreach (var item in dict.Values)
            {
                worksheet.Cells[row, 1].Value = k;
                worksheet.Cells[row, 2].Value = item.OrganizationUnitName;
                worksheet.Cells[row, 3].Value = item.Date.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cells[row, 4].Value = item.WeldingMachineMac;
                worksheet.Cells[row, 5].Value = item.WeldingMachineName;
                worksheet.Cells[row, 6].Value = item.WeldingMachineTypeName;
                worksheet.Cells[row, 7].Value = item.ErrorCode;

                // Duration
                ts = TimeSpan.FromMilliseconds(item.DurationMs);
                worksheet.Cells[row, 8].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 8].Value = ts;

                worksheet.Cells[row, 9].Value = item.UserAccountName;
                worksheet.Cells[row, 10].Value = item.WeldingProgramName;

                // Working time
                ts = TimeSpan.FromSeconds(item.WorkingTimeAfterServiceSecs);
                worksheet.Cells[row, 11].Style.Numberformat.Format = "hh:mm:ss";
                worksheet.Cells[row, 11].Value = ts;

                row++;
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
