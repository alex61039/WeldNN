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
    public class ReportGenerator_General : IReportGenerator
    {
        // WeldingContext _context;
        IWeldingContextFactory _weldingContextFactory;
        FlowsCalculator flowsCalculator;

        public ReportGenerator_General(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
            flowsCalculator = new FlowsCalculator(weldingContextFactory);
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

        private SortedDictionary<string, GeneralReportItem> PrepareData_GeneralReport(ReportRequest req)
        {
            var dict = new SortedDictionary<string, GeneralReportItem>();

            // Dictionary of configs
            var configs = new Dictionary<int, WeldingMachineTypeConfiguration>();


            // Time
            TimeSpan? timeFrom = !String.IsNullOrEmpty(req.TimeFrom) ? TimeSpan.Parse(req.TimeFrom) : (TimeSpan?)null;
            TimeSpan? timeTo = !String.IsNullOrEmpty(req.TimeTo) ? TimeSpan.Parse(req.TimeTo) : (TimeSpan?)null;

            System.Data.Entity.Core.Objects.ObjectResult<Report_General_Result> data = null;
            using (var _context2 = _weldingContextFactory.CreateContext(18000))
            using (var transaction = _context2.Database.BeginTransaction(System.Data.IsolationLevel.ReadUncommitted))
            {
                data = _context2.Report_General(
                    req.DateFrom,
                    req.DateTo,
                    req.UserAccountID.GetValueOrDefault() == 0 ? (int?)null : req.UserAccountID,
                    req.OrganizationUnitID.GetValueOrDefault() == 0 ? (int?)null : req.OrganizationUnitID,
                    req.WeldingMachineID.GetValueOrDefault() == 0 ? (int?)null : req.WeldingMachineID
                );

                foreach (var d in data)
                {
                    // Check Organization
                    if (!req.OrganizationUnitIDs.Contains(d.OrganizationUnitID.GetValueOrDefault()))
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

                    var machine = getWeldingMachine(d.WeldingMachineID);

                    var item = new GeneralReportItem
                    {
                        OrganizaionUnitID = d.OrganizationUnitID.Value,
                        OrganizationUnitName = d.OrganizationUnitName,
                        Date = d.DateCreated,
                        UserAccountID = d.UserAccountID,
                        UserAccountName = d.UserAccountName,
                        WeldingMachineID = d.WeldingMachineID,
                        WeldingMachineName = machine != null ? machine.Name : "",

                        WeldingMachineMac = d.WeldingMachineMAC,
                        WeldingMachineTypeName = machine != null && machine.WeldingMachineType != null ? machine.WeldingMachineType.Name : "",
                        WeldingMachineTypeID = machine != null ? machine.WeldingMachineTypeID : 0,

                        TotalTimeMs = d.StateDurationMs,
                        WorkTimeMs = status == WeldingMachineStatus.Working ? d.StateDurationMs : 0,
                        StandbyTimeMs = status == WeldingMachineStatus.Ready ? d.StateDurationMs : 0,
                        ErrorTimeMs = (status == WeldingMachineStatus.Error || !String.IsNullOrEmpty(d.ErrorCode)) ? d.StateDurationMs : 0,

                        Wire = 0,
                        Gas = 0,
                        Electricity = 0,
                    };

                    // Расход электричества
                    item.Electricity = flowsCalculator.CalculateElectricity(config, d);

                    // Wire/Расход прволоки (кг/мин)
                    // только в режиме сварки
                    item.Wire = flowsCalculator.CalculateWireFlow(d);

                    // Gas/Расход газа (литры)
                    // только в режиме сварки
                    item.Gas = flowsCalculator.CalculateGasFlow(d);



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

                        existing_item.TotalTimeMs += item.TotalTimeMs;
                        existing_item.WorkTimeMs += item.WorkTimeMs;
                        existing_item.StandbyTimeMs += item.StandbyTimeMs;
                        existing_item.ErrorTimeMs += item.ErrorTimeMs;
                        existing_item.Wire += item.Wire;
                        existing_item.Gas += item.Gas;
                        existing_item.Electricity += item.Electricity;

                        dict[key] = existing_item;
                    }
                }
            }


            // Add zero-row if no data
            if (dict == null || dict.Count == 0)
            {
                dict = new SortedDictionary<string, GeneralReportItem>();

                // add empty rows with welding machines
                using (var _context = _weldingContextFactory.CreateContext(0))
                {
                    var machines = _context.WeldingMachines.Where(m => m.Status == (int)GeneralStatus.Active && req.OrganizationUnitIDs.Contains(m.OrganizationUnitID));

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
                }

                // Still no welding machines?
                if (dict.Count == 0)
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

            return dict;
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
            byte[] fileContents;

            using (var package = new ExcelPackage())
            {
                var worksheet = BuildWorksheet(package, req, false);

                // Finally when you're done, export it to byte array.
                fileContents = package.GetAsByteArray();
            }


            return new ReportGeneratorResult { ExcelData = fileContents };
        }

        public ExcelWorksheet BuildWorksheet(ExcelPackage package, ReportRequest req, bool buildGraph = false)
        {
            // Prepare data
            var dict = PrepareData_GeneralReport(req);

            ExcelWorksheet worksheet_graph = buildGraph ? package.Workbook.Worksheets.Add("Graph") : null;

            var worksheet = package.Workbook.Worksheets.Add("Sheet1");

            // Prepare Date/times
            var dt_from = req.DateFrom.Value.Date;
            var dt_to = req.DateTo.Value.Date;

            // TITLE
            worksheet.Cells[1, 1, 1, 5].Merge = true;
            worksheet.Cells[1, 1].Value = buildGraph ? "Данные для диаграммы" : "Загрузка оборудования";
            worksheet.Cells[1, 1].Style.Font.Size = 20;
            worksheet.Cells[1, 1].Style.Font.Bold = true;

            // Dates range
            var title = dt_from == dt_to ? req.DateFrom.Value.ToString("dd-MM-yyyy")
                : String.Format("{0} - {1}", req.DateFrom.Value.ToString("dd-MM-yyyy"), req.DateTo.Value.ToString("dd-MM-yyyy"));

            // Time
            TimeSpan? timeFrom = !String.IsNullOrEmpty(req.TimeFrom) ? TimeSpan.Parse(req.TimeFrom) : (TimeSpan?)null;
            TimeSpan? timeTo = !String.IsNullOrEmpty(req.TimeTo) ? TimeSpan.Parse(req.TimeTo) : (TimeSpan?)null;

            // Time range
            if (!String.IsNullOrEmpty(req.TimeFrom) && !String.IsNullOrEmpty(req.TimeTo))
                title += String.Format(", {0} - {1}", req.TimeFrom, req.TimeTo);
            else if (!String.IsNullOrEmpty(req.TimeFrom))
                title += String.Format(", с {0}", req.TimeFrom);
            else if (!String.IsNullOrEmpty(req.TimeTo))
                title += String.Format(", до {0}", req.TimeTo);


            worksheet.Cells[2, 1].Value = title;
            worksheet.Cells[2, 1].Style.Font.Bold = true;
            worksheet.Cells[2, 1].Style.Font.Size = 16;


            // Header
            int headerRow = 4;
            int row = headerRow;
            worksheet.Row(row).Style.Font.Bold = true;
            // worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Value = "#";
            worksheet.Cells[row, 2].Value = "Цех";
            worksheet.Cells[row, 3].Value = "Дата/время";
            worksheet.Cells[row, 4].Value = "MAC адрес";
            worksheet.Cells[row, 5].Value = "Аппарат";
            worksheet.Cells[row, 6].Value = "Тип аппарата";
            worksheet.Cells[row, 7].Value = "Сварщик";
            worksheet.Cells[row, 8].Value = "Наработка, ч:м:с";
            worksheet.Cells[row, 9].Value = "Сварка, ч:м:с";
            worksheet.Cells[row, 10].Value = "Простой, ч:м:с";
            worksheet.Cells[row, 11].Value = "Неисправн., ч:м:с";
            worksheet.Cells[row, 12].Value = "Расход проволоки, кг";
            worksheet.Cells[row, 13].Value = "Расход газа, л";
            worksheet.Cells[row, 14].Value = "Расход эл. энергии, кВт.ч.";

            worksheet.Column(1).Width = 5;
            worksheet.Column(2).Width = 25;
            worksheet.Column(3).Width = 15;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 20;
            worksheet.Column(6).Width = 20;
            worksheet.Column(7).Width = 20;
            worksheet.Column(8).Width = 15;
            worksheet.Column(9).Width = 15;
            worksheet.Column(10).Width = 15;
            worksheet.Column(11).Width = 15;
            worksheet.Column(12).Width = 15;
            worksheet.Column(13).Width = 15;
            worksheet.Column(14).Width = 15;

            // Data
            row++;
            int k = 1;
            TimeSpan ts;
            foreach (var item in dict.Values)
            {
                worksheet.Cells[row, 1].Value = k;
                worksheet.Cells[row, 2].Value = item.OrganizationUnitName;
                worksheet.Cells[row, 3].Value = item.Date.HasValue ? item.Date.Value.ToString("yyyy-MM-dd") : "";
                worksheet.Cells[row, 4].Value = item.WeldingMachineMac;
                worksheet.Cells[row, 5].Value = item.WeldingMachineName;
                worksheet.Cells[row, 6].Value = item.WeldingMachineTypeName;
                worksheet.Cells[row, 7].Value = item.UserAccountName;

                //worksheet.Cells[k + 1, 6].Value = (item.TotalTimeMs / 1000.0 / 60.0 / 60.0).ToString("F2"); // Hours
                //worksheet.Cells[k + 1, 7].Value = (item.WorkTimeMs / 1000.0 / 60.0 / 60.0).ToString("F2");    // Hours
                //worksheet.Cells[k + 1, 8].Value = (item.StandbyTimeMs / 1000.0 / 60.0 / 60.0).ToString("F2");    // Hours
                //worksheet.Cells[k + 1, 9].Value = (item.ErrorTimeMs / 1000.0 / 60.0 / 60.0).ToString("F2");    // Hours

                ts = TimeSpan.FromMilliseconds(item.TotalTimeMs);
                worksheet.Cells[row, 8].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 8].Value = ts;

                ts = TimeSpan.FromMilliseconds(item.WorkTimeMs);
                worksheet.Cells[row, 9].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 9].Value = ts;

                ts = TimeSpan.FromMilliseconds(item.StandbyTimeMs);
                worksheet.Cells[row, 10].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 10].Value = ts;

                ts = TimeSpan.FromMilliseconds(item.ErrorTimeMs);
                worksheet.Cells[row, 11].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 11].Value = ts;

                worksheet.Cells[row, 12].Value = item.Wire;
                worksheet.Cells[row, 12].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 13].Value = item.Gas;
                worksheet.Cells[row, 13].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 14].Value = item.Electricity;
                worksheet.Cells[row, 14].Style.Numberformat.Format = "0.00";

                row++;
                k++;
            }

            // Total row
            if (dict.Count > 0)
            {
                worksheet.Row(row).Style.Font.Bold = true;

                worksheet.Cells[row, 7].Value = "Итого:";

                ts = TimeSpan.FromMilliseconds(dict.Values.Sum(item => item.TotalTimeMs));
                worksheet.Cells[row, 8].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 8].Value = ts;

                ts = TimeSpan.FromMilliseconds(dict.Values.Sum(item => item.WorkTimeMs));
                worksheet.Cells[row, 9].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 9].Value = ts;

                ts = TimeSpan.FromMilliseconds(dict.Values.Sum(item => item.StandbyTimeMs));
                worksheet.Cells[row, 10].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 10].Value = ts;

                ts = TimeSpan.FromMilliseconds(dict.Values.Sum(item => item.ErrorTimeMs));
                worksheet.Cells[row, 11].Style.Numberformat.Format = "[h]:mm:ss";
                worksheet.Cells[row, 11].Value = ts;

                worksheet.Cells[row, 12].Value = dict.Values.Sum(item => item.Wire);
                worksheet.Cells[row, 12].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 13].Value = dict.Values.Sum(item => item.Gas);
                worksheet.Cells[row, 13].Style.Numberformat.Format = "0.00";
                worksheet.Cells[row, 14].Value = dict.Values.Sum(item => item.Electricity);
                worksheet.Cells[row, 14].Style.Numberformat.Format = "0.00";

            }

            if (buildGraph)
            {
                worksheet_graph = BuildGraph(worksheet, worksheet_graph, headerRow, row, req);
            }

            return worksheet;
        }

        public ExcelWorksheet BuildGraph(ExcelWorksheet worksheet, ExcelWorksheet worksheet_graph, int rowHeader, int rowTotal, ReportRequest req)
        {
            // TITLE
            worksheet_graph.Cells[1, 1, 1, 10].Merge = true;
            worksheet_graph.Cells[1, 1].Value = "Диаграмма загрузки оборудования";
            worksheet_graph.Cells[1, 1].Style.Font.Size = 20;
            worksheet_graph.Cells[1, 1].Style.Font.Bold = true;

            // Dates range
            var title = "";
            if (req.DateFrom.HasValue && req.DateTo.HasValue)
                title = req.DateFrom.Value.Date == req.DateTo.Value.Date ? String.Format("{0}", req.DateFrom.Value.ToString("dd-MM-yyyy"))
                    : String.Format("{0} - {1}", req.DateFrom.Value.ToString("dd-MM-yyyy"), req.DateTo.Value.ToString("dd-MM-yyyy"));
            else if (req.DateFrom.HasValue)
                title = String.Format("С {0}", req.DateFrom.Value.ToString("dd-MM-yyyy"));
            else if (req.DateTo.HasValue)
                title = String.Format("По {0}", req.DateTo.Value.ToString("dd-MM-yyyy"));


            // Time
            TimeSpan? timeFrom = !String.IsNullOrEmpty(req.TimeFrom) ? TimeSpan.Parse(req.TimeFrom) : (TimeSpan?)null;
            TimeSpan? timeTo = !String.IsNullOrEmpty(req.TimeTo) ? TimeSpan.Parse(req.TimeTo) : (TimeSpan?)null;

            // Time range
            if (!String.IsNullOrEmpty(req.TimeFrom) && !String.IsNullOrEmpty(req.TimeTo))
                title += String.Format(", {0} - {1}", req.TimeFrom, req.TimeTo);
            else if (!String.IsNullOrEmpty(req.TimeFrom))
                title += String.Format(", с {0}", req.TimeFrom);
            else if (!String.IsNullOrEmpty(req.TimeTo))
                title += String.Format(", до {0}", req.TimeTo);


            worksheet_graph.Cells[2, 1].Value = title;
            worksheet_graph.Cells[2, 1].Style.Font.Bold = true;
            worksheet_graph.Cells[2, 1].Style.Font.Size = 16;

            if (req.WeldingMachineID.HasValue && req.WeldingMachineID.Value > 0)
            {
                var machine = getWeldingMachine(req.WeldingMachineID.Value);
                worksheet_graph.Cells[3, 1].Value = machine.Name;
            }


            // --------------------------------------------------------------------------------------------------
            //create a new piechart of type Pie3D
            ExcelPieChart pieChart = worksheet_graph.Drawings.AddChart("Загрузка оборудования", eChartType.Pie3D) as ExcelPieChart;

            //set the title
            pieChart.Title.Text = "Загрузка оборудования";

            //select the ranges for the pie. First the values, then the header range
            // pieChart.Series.Add(ExcelRange.GetAddress(21, 11, 22, 11), ExcelRange.GetAddress(21, 10, 22, 10));

            var dataRange = worksheet.Cells[rowTotal, 9, rowTotal, 11];
            var headerRange = worksheet.Cells[rowHeader, 9, rowHeader, 11];
            pieChart.Series.Add(dataRange, headerRange);

            //pieChart.Series.Add(
            //    worksheet.Cells[rowTotal, 9, rowTotal, 9], 
            //    worksheet.Cells[rowHeader, 9, rowHeader, 9]);
            //pieChart.Series.Add(
            //    worksheet.Cells[rowTotal, 10, rowTotal, 10],
            //    worksheet.Cells[rowHeader, 10, rowHeader, 10]);
            //pieChart.Series.Add(
            //    worksheet.Cells[rowTotal, 11, rowTotal, 11],
            //    worksheet.Cells[rowHeader, 11, rowHeader, 11]);

            var series = pieChart.Series[0];
            // pieChart.Series[0].Fill.Color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Orange);

            // pieChart.VaryColors = true;
            // pieChart.Fill.Color.


            /*
            // worksheet.Cells[row, 9].Value = "Сварка, ч:м:с";
            pieChart.Series[0].Fill.Color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Orange);
            // worksheet.Cells[row, 10].Value = "Простой, ч:м:с";
            pieChart.Series[1].Fill.Color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Green);
            // worksheet.Cells[row, 11].Value = "Неисправн., ч:м:с";
            pieChart.Series[2].Fill.Color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Red);
            */

            //position of the legend
            pieChart.Legend.Position = eLegendPosition.Right;

            //show the percentages in the pie
            pieChart.DataLabel.ShowPercent = true;
            pieChart.DataLabel.ShowValue = true;

            //size of the chart
            pieChart.SetSize(800, 600);

            //add the chart at cell
            pieChart.SetPosition(4, 0, 1, 0);


            return worksheet_graph;
        }
    }
}
