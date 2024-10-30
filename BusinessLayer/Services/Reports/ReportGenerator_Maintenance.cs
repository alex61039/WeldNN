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
    public class ReportGenerator_Maintenance : IReportGenerator
    {
        // WeldingContext _context;
        IWeldingContextFactory _weldingContextFactory;
        // WeldingMachineTypeConfigurationLoader _configLoader;

        public ReportGenerator_Maintenance(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
            // _configLoader = new WeldingMachineTypeConfigurationLoader(_context);
        }


        class ReportItem
        {
            public DateTime? Date;

            public int OrganizaionUnitID;
            public String OrganizationUnitName;
            public int? UserAccountID;
            public string UserAccountName;

            public int WeldingMachineID;
            public string WeldingMachineName;
            public string WeldingMachineMac;
            public int WeldingMachineTypeID;
            public string WeldingMachineTypeName;
            public DateTime? WeldingMachineStartUsing;

            public long TimeTotalSecs;
            public long TimeAfterLastService;
            public long TimeTillNextServiceSecs;

            public DateTime? NotificationSentOn;

            public DateTime? MaintenanceFinishedOn;

            public string Key()
            {
                return string.Format("{0}_{1}",
                    Date.HasValue ? Date.Value.ToString("yyyyMMdd_HHmmss") : "",
                    WeldingMachineID
                    );
            }
        }

        private ReportItem CreateReportItem(Maintenance d)
        {
            // worker user
            var user = getUserAccount(d.ResponsibleUserID.GetValueOrDefault());
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
                WeldingMachineStartUsing = d.WeldingMachine.DateStartedUsing,

                TimeTotalSecs = d.TotalTimeSec.GetValueOrDefault(),
                TimeAfterLastService = d.WeldingMachine.TimeAfterLastServiceSecs.GetValueOrDefault(),
                TimeTillNextServiceSecs = d.WeldingMachine.TimeTillNextServiceSecs.GetValueOrDefault(),

                NotificationSentOn = d.WeldingMachine.UserServiceNotifiedOn,

                MaintenanceFinishedOn = d.DateFinished.Value
            };

            return item;
        }

        private SortedDictionary<string, ReportItem> prepareReportData(ReportRequest req)
        {
            var dict = new SortedDictionary<string, ReportItem>();

            // DATA
            var machineIDs = new List<int>();
            using (var __context = _weldingContextFactory.CreateContext(18000))
            {
                var query = __context.Maintenances
                    .Where(s => s.Status == (int)GeneralStatus.Active
                        && s.MaintenanceStatus == (int)MaintenanceStatus.Completed)
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


                // Organization unit
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
                    // Create report item
                    var item = CreateReportItem(d);

                    // Exists?
                    var key = item.Key();
                    if (!dict.ContainsKey(key))
                    {
                        // Add new item
                        dict.Add(key, item);
                    }

                    if (!machineIDs.Contains(d.WeldingMachineID))
                        machineIDs.Add(d.WeldingMachineID);
                }
            }


            // Add machines missing in report
            using (var __context = _weldingContextFactory.CreateContext(18000))
            {
                var machines_query = __context.WeldingMachines
                .Where(m => m.Status == (int)GeneralStatus.Active
                    && !machineIDs.Contains(m.ID)
                    && req.OrganizationUnitIDs.Contains(m.OrganizationUnitID))
                .Include(m => m.OrganizationUnit)
                .Include(m => m.WeldingMachineType);

                // Organization unit
                if (req.OrganizationUnitID.HasValue && req.OrganizationUnitID.Value > 0)
                    machines_query = machines_query.Where(s => s.OrganizationUnitID == req.OrganizationUnitID.Value);

                //Machine
                if (req.WeldingMachineID.HasValue && req.WeldingMachineID.Value > 0)
                    machines_query = machines_query.Where(s => s.ID == req.WeldingMachineID.Value);


                machines_query = machines_query.OrderBy(m => m.Name);

                foreach (var m in machines_query)
                {
                    var item = new ReportItem
                    {
                        Date = null,

                        OrganizaionUnitID = m.OrganizationUnitID,
                        OrganizationUnitName = m.OrganizationUnit.Name,
                        UserAccountID = (int?)null,
                        UserAccountName = "",

                        WeldingMachineID = m.ID,
                        WeldingMachineName = m.Name,
                        WeldingMachineMac = m.MAC,
                        WeldingMachineTypeID = m.WeldingMachineTypeID,
                        WeldingMachineTypeName = m.WeldingMachineType.Name,
                        WeldingMachineStartUsing = m.DateStartedUsing,

                        TimeTotalSecs = m.TimeTotalSecs.GetValueOrDefault(),
                        TimeAfterLastService = m.TimeAfterLastServiceSecs.GetValueOrDefault(),
                        TimeTillNextServiceSecs = m.TimeTillNextServiceSecs.GetValueOrDefault(),

                        NotificationSentOn = null,

                        MaintenanceFinishedOn = null
                    };

                    var key = item.Key();
                    if (!dict.ContainsKey(key))
                    {
                        // Add new item
                        dict.Add(key, item);
                    }
                }
            }

            return dict;
        }

        Dictionary<int, UserAccount> dictUsers;
        UserAccount getUserAccount(int userID)
        {
            if (userID <= 0)
                return null;

            if (dictUsers == null)
                dictUsers = new Dictionary<int, UserAccount>();

            if (dictUsers.ContainsKey(userID))
                return dictUsers[userID];

            // Load from db
            using (var __context = _weldingContextFactory.CreateContext(0))
            {
                var user = __context.UserAccounts.FirstOrDefault(u => u.ID == userID);
                if (user == null)
                    return null;

                dictUsers[userID] = user;

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
            worksheet.Cells[1, 1, 1, 11].Merge = true;
            worksheet.Cells[1, 1].Value = "Отчет о сервисном обслуживании";
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
            worksheet.Row(row).Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            // worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Value = "#";
            worksheet.Cells[row, 2].Value = "Цех";
            worksheet.Cells[row, 3].Value = "MAC адрес";
            worksheet.Cells[row, 4].Value = "Аппарат";
            worksheet.Cells[row, 5].Value = "Тип аппарата";
            worksheet.Cells[row, 6].Value = "Дата ввода в эксплуатацию";
            worksheet.Cells[row, 7].Value = "Наработка всего на дату обслуживания, ч:м";
            worksheet.Cells[row, 8].Value = "Наработка после предыдущего обслуживания, ч:м";
            worksheet.Cells[row, 9].Value = "До следующего обслуживания, ч:м";
            worksheet.Cells[row, 10].Value = "Дата отправки уведомления об обслуживании";
            worksheet.Cells[row, 11].Value = "Дата отметки о проведенном обслуживании";
            worksheet.Cells[row, 12].Value = "Ответственный сотрудник за сервис";

            worksheet.Column(1).Width = 5;
            worksheet.Column(2).Width = 30;
            worksheet.Column(3).Width = 20;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 25;
            worksheet.Column(6).Width = 20;

            worksheet.Column(7).Width = 25;
            worksheet.Column(7).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            worksheet.Column(8).Width = 25;
            worksheet.Column(8).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            worksheet.Column(9).Width = 20;
            worksheet.Column(9).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

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
                worksheet.Cells[row, 1].Value = k;
                worksheet.Cells[row, 2].Value = item.OrganizationUnitName;
                worksheet.Cells[row, 3].Value = item.WeldingMachineMac;
                worksheet.Cells[row, 4].Value = item.WeldingMachineName;
                worksheet.Cells[row, 5].Value = item.WeldingMachineTypeName;
                worksheet.Cells[row, 6].Value = item.WeldingMachineStartUsing.HasValue ? item.WeldingMachineStartUsing.Value.ToString("yyyy-MM-dd") : "";

                // Наработка всего
                // ts = TimeSpan.FromSeconds(item.TimeTotalSecs);
                // worksheet.Cells[row, 7].Style.Numberformat.Format = "hh:mm";
                worksheet.Cells[row, 7].Value = Utils.ReportsDateTimeFormats.TotalTime(item.TimeTotalSecs);

                // Наработка с последнего обслуживания
                // ts = TimeSpan.FromSeconds(item.TimeAfterLastService);
                // worksheet.Cells[row, 8].Style.Numberformat.Format = "hh:mm";
                worksheet.Cells[row, 8].Value = Utils.ReportsDateTimeFormats.TotalTime(item.TimeAfterLastService);

                // До след. обслуживания
                // ts = TimeSpan.FromSeconds(Math.Abs(item.TimeTillNextServiceSecs));
                // worksheet.Cells[row, 9].Style.Numberformat.Format = item.TimeTillNextServiceSecs >= 0 ? "hh:mm" : "(hh:mm)";
                if (item.TimeTillNextServiceSecs < 0)
                    worksheet.Cells[row, 9].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                worksheet.Cells[row, 9].Value = Utils.ReportsDateTimeFormats.TotalTime(item.TimeTillNextServiceSecs);

                worksheet.Cells[row, 10].Value = item.NotificationSentOn.HasValue ? item.NotificationSentOn.Value.ToString("yyyy-MM-dd") : "";
                worksheet.Cells[row, 11].Value = item.MaintenanceFinishedOn.HasValue ? item.MaintenanceFinishedOn.Value.ToString("yyyy-MM-dd") : "";


                worksheet.Cells[row, 12].Value = item.UserAccountName;


                row++;
                k++;
            }

            // Total row
            if (dict.Count > 0)
            {
            }
            else
            {
                worksheet.Cells[row, 2].Value = "Нет данных за выбранный период";
            }

            return worksheet;
        }

    }
}
