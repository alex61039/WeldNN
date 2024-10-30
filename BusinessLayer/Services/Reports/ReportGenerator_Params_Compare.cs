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
    public class ReportGenerator_Params_Compare : IReportGenerator
    {
        // WeldingContext _context;
        IWeldingContextFactory _weldingContextFactory;

        public ReportGenerator_Params_Compare(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
        }

        /// <summary>
        /// Parameters: DateFrom, DateTo, TimeFrom?, TimeTo?, WeldingMachineTypeID, WeldingMachineIDs?, PropertyCodes
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public ReportGeneratorResult Generate(ReportRequest req)
        {
            byte[] fileContents;

            // Validate request
            //if (!req.DateFrom.HasValue || !req.DateTo.HasValue)
            //{
            //    return null;
            //}
            if (!req.Date.HasValue)
            {
                return null;
            }

            using (var package = new ExcelPackage())
            {
                var worksheet = BuildWorksheet(package, req);

                // Finally when you're done, export it to byte array.
                fileContents = package.GetAsByteArray();
            }


            return new ReportGeneratorResult { ExcelData = fileContents };
        }

        public ExcelWorksheet BuildWorksheet(ExcelPackage package, ReportRequest req, bool buildGraph = false)
        {
            ExcelWorksheet worksheet_graph = buildGraph ? package.Workbook.Worksheets.Add("Graph") : null;

            var worksheet = package.Workbook.Worksheets.Add("Sheet1");


            // Retrieve machine/type/conf
            WeldingMachineTypeConfiguration conf = null;
            using (var _context = _weldingContextFactory.CreateContext(0))
            {
                WeldingMachineTypeConfigurationLoader confLoader = new WeldingMachineTypeConfigurationLoader(_context);
                conf = confLoader.LoadByType(req.WeldingMachineTypeID.Value);
            }

            

            // WeldingMachineIDs
            int[] WeldingMachineIDs;
            if (req.WeldingMachineIDs != null && req.WeldingMachineIDs.Count > 0)
            {
                WeldingMachineIDs = req.WeldingMachineIDs.ToArray();
            }
            else
            {
                // Все аппараты выбранного типа
                using (var _context = _weldingContextFactory.CreateContext(0))
                {
                    WeldingMachineIDs = _context.WeldingMachines
                        .Where(m => m.Status == (int)GeneralStatus.Active && m.WeldingMachineTypeID == req.WeldingMachineTypeID.Value && req.OrganizationUnitIDs.Contains(m.OrganizationUnitID))
                        .OrderBy(m => m.Name)
                        .Select(m => m.ID)
                        .ToArray();
                }
            }

            // WeldingMachines
            List<WeldingMachine> WeldingMachines = null;
            using (var _context = _weldingContextFactory.CreateContext(0))
            {
                WeldingMachines = _context.WeldingMachines
                .Where(m => m.Status == (int)GeneralStatus.Active
                    && WeldingMachineIDs.Contains(m.ID)
                    && m.WeldingMachineTypeID == req.WeldingMachineTypeID
                    && req.OrganizationUnitIDs.Contains(m.OrganizationUnitID))
                .ToList();
            }


            // Convert state to Panel Summary Info
            List<Models.WeldingMachine.SummaryProperty> summaryPropsAll = null;
            using (var __context = _weldingContextFactory.CreateContext(0))
            {
                var panelStateBuilder = new PanelStateBuilder(conf, __context);
                summaryPropsAll = panelStateBuilder.BuildSummaryProperties(null, true);
            }
            if (summaryPropsAll == null)
                summaryPropsAll = new List<Models.WeldingMachine.SummaryProperty>();

            // Filter summary props if requested
            List<Models.WeldingMachine.SummaryProperty> summaryPropsFiltered = summaryPropsAll;
            if (req.PropertyCodes != null && req.PropertyCodes.Count > 0)
            {
                summaryPropsFiltered = summaryPropsAll
                    .Where(p => req.PropertyCodes.Contains(p.PropertyCode))
                    .ToList();
            }



            // Prepare Date/times
            var dt_from = req.Date.Value.Date;
            var dt_to = req.Date.Value.Date;
            var dt_to_nextDay = req.Date.Value.Date.AddDays(1);

            TimeSpan? timeFrom = !String.IsNullOrEmpty(req.TimeFrom) ? TimeSpan.Parse(req.TimeFrom) : (TimeSpan?)null;
            TimeSpan? timeTo = !String.IsNullOrEmpty(req.TimeTo) ? TimeSpan.Parse(req.TimeTo) : (TimeSpan?)null;


            // TITLE
            worksheet.Cells[1, 1, 1, 5].Merge = true;
            worksheet.Cells[1, 1].Value = buildGraph ? "Данные для графика" : "Сравнительный отчет по параметрам";
            worksheet.Cells[1, 1].Style.Font.Size = 20;
            worksheet.Cells[1, 1].Style.Font.Bold = true;

            // Dates range
            //var title = dt_from == dt_to ? req.DateFrom.Value.ToString("dd-MM-yyyy")
            //    : String.Format("{0} - {1}", req.DateFrom.Value.ToString("dd-MM-yyyy"), req.DateTo.Value.ToString("dd-MM-yyyy"));
            var title = req.Date.Value.ToString("dd-MM-yyyy");

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

            worksheet.Column(1).Width = 20;

            int data_start_row = 0;
            int total = 0;
            var propertyColumn = new Dictionary<string, Dictionary<int, int>>();    // PropertyCode, WeldingMachineID

            using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required
               , new TransactionOptions
               {
                   IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted,
                   Timeout = TimeSpan.FromMinutes(15)
               }))
            {
                // Fetch all States on this days
                var _stateIDs = new[] { new { ID = 0, WeldingMachineID = 0, DateCreated = DateTime.Now, StateDurationMs = 0 } }; 
                using (var __context = _weldingContextFactory.CreateContext(18000))
                {
                    _stateIDs = __context.WeldingMachineStates
                        .Where(s =>
                            WeldingMachineIDs.Contains(s.WeldingMachineID)
                            && s.DateCreated >= dt_from
                            && s.DateCreated < dt_to_nextDay
                        )
                        .OrderBy(s => s.DateCreated)
                        .Select(s => new
                        {
                            ID = s.ID,
                            WeldingMachineID = s.WeldingMachineID,
                            DateCreated = s.DateCreated,
                            StateDurationMs = s.StateDurationMs
                        })
                        .ToArray();
                }

                // Add Duration to DateCreated
                var stateIDs = _stateIDs
                .Select(s => new
                {
                    ID = s.ID,
                    WeldingMachineID = s.WeldingMachineID,
                    DateCreated = s.DateCreated,
                    StateDurationMs = s.StateDurationMs,
                    DateEnd = s.DateCreated.AddMilliseconds(s.StateDurationMs + 500)    // add 0.5 second
                    })
                .ToArray();



                // Build Header
                var headerRow = 4;
                int col = 2;
                foreach (var p in summaryPropsFiltered)
                {
                    propertyColumn[p.PropertyCode] = new Dictionary<int, int>();

                    foreach (var m in WeldingMachines)
                    {
                        worksheet.Column(col).Width = 20;

                        // Аппарат
                        worksheet.Cells[headerRow, col].Value = m.Name;
                        worksheet.Cells[headerRow, col].Style.Font.Bold = true;

                        // Аппарат, макадрес
                        worksheet.Cells[headerRow + 1, col].Value = m.MAC;
                        worksheet.Cells[headerRow + 1, col].Style.Font.Bold = true;

                        // Название свойства
                        worksheet.Cells[headerRow + 2, col].Value = p.Title + (String.IsNullOrEmpty(p.Unit) ? "" : (" (" + p.Unit + ")"));
                        worksheet.Cells[headerRow + 2, col].Style.Font.Bold = true;

                        // Код свойства
                        //worksheet.Cells[headerRow + 3, col].Value = p.PropertyCode;
                        //worksheet.Cells[headerRow + 3, col].Style.Font.Bold = true;

                        propertyColumn[p.PropertyCode][m.ID] = col;
                        col++;
                    }

                }


                data_start_row = headerRow + 3;
                var row = data_start_row;
                var anyData = false;

                // Iterate by days
                var dt = req.DateFrom.Value.Date;
                while (dt <= dt_to)
                {
                    if (!stateIDs.Any(s => s.DateCreated.Date == dt))
                    {
                        dt = dt.AddDays(1);
                        continue;
                    }

                    // Get min/max time
                    var time_min = stateIDs
                         .Where(s => s.DateCreated.Date == dt)
                         .Select(s => s.DateCreated.TimeOfDay)
                         .Min();

                    var time_max = stateIDs
                         .Where(s => s.DateEnd.Date == dt)
                         .Select(s => s.DateEnd.TimeOfDay)
                         .Max();

                    if (timeFrom.HasValue)
                        time_min = timeFrom.Value;
                        // time_min = time_min < timeFrom.Value ? time_min : timeFrom.Value;

                    if (timeTo.HasValue)
                        time_max = timeTo.Value;
                        // time_max = time_max > timeTo.Value ? time_max : timeTo.Value;

                    // Iterate by seconds
                    var ts = time_min;
                    while (ts <= time_max)
                    {
                        // Date time
                        var row_dt = dt.Date + ts;
                        worksheet.Cells[row, 1].Value = row_dt.ToString("dd-MM-yyyy HH:mm:ss");
                        total++;

                        // Is there any state within this time
                        var states = stateIDs.Where(s => s.DateCreated.Date == dt
                            && s.DateCreated.TimeOfDay <= ts && s.DateEnd.TimeOfDay >= ts);

                        if (states.Any())
                        {
                            foreach (var state in states)
                            {
                                // Fetch State
                                Models.WeldingMachine.StateSummary machineState = null;
                                using (var __context = _weldingContextFactory.CreateContext(0)) {
                                    var _machineStateService = new MachineStateService(__context);
                                    machineState = _machineStateService.LoadState(state.ID);
                                }

                                // Check that machine is ON
                                if (machineState == null 
                                    || (machineState.Status != WeldingMachineStatus.Ready && machineState.Status != WeldingMachineStatus.Working && machineState.Status != WeldingMachineStatus.Error))
                                    continue;

                                // Convert state to Panel Summary Info
                                List<Models.WeldingMachine.SummaryProperty> machineStateProps = null;
                                using (var __context = _weldingContextFactory.CreateContext(0))
                                {
                                    var panelStateBuilder = new PanelStateBuilder(conf, __context);
                                    machineStateProps = panelStateBuilder.BuildSummaryProperties(machineState, true);
                                }
                                if (machineStateProps == null)
                                    continue;

                                // Fill values
                                foreach (var p in machineStateProps)
                                {
                                    if (propertyColumn.ContainsKey(p.PropertyCode)
                                        && propertyColumn[p.PropertyCode].ContainsKey(state.WeldingMachineID))
                                    {
                                        col = propertyColumn[p.PropertyCode][state.WeldingMachineID];

                                        // VALUE
                                        if (p.PropertyType == "number")
                                        {
                                            if (double.TryParse(p.Value, out double d))
                                                worksheet.Cells[row, col].Value = d;
                                            else
                                                worksheet.Cells[row, col].Value = p.Value;
                                        }
                                        else
                                        {
                                            worksheet.Cells[row, col].Value = translate(p.PropertyCode, p.Value);
                                        }
                                    }
                                }


                            }

                        }

                        // Add 1 second
                        ts = ts.Add(TimeSpan.FromSeconds(1));
                        row++;
                        anyData = true;
                    }


                    dt = dt.AddDays(1);
                }


                scope.Complete();
            }

            if (buildGraph)
            {
                worksheet_graph = BuildGraph(worksheet, worksheet_graph, data_start_row, total, req, propertyColumn);
            }

            return worksheet;
        }

        public ExcelWorksheet BuildGraph(ExcelWorksheet worksheet, ExcelWorksheet worksheet_graph, int rowStart, int rowsTotal,
            ReportRequest req, Dictionary<string, Dictionary<int, int>> propertyColumn)
        {
            // TITLE
            worksheet_graph.Cells[1, 1, 1, 5].Merge = true;
            worksheet_graph.Cells[1, 1].Value = "Регистрограмма";
            worksheet_graph.Cells[1, 1].Style.Font.Size = 20;
            worksheet_graph.Cells[1, 1].Style.Font.Bold = true;

            // Date/times range (X-axis)
            var range_titles = worksheet.Cells[rowStart, 1, rowStart + rowsTotal - 1, 1];


            int k = 0;
            foreach (var weldingMachineID in req.WeldingMachineIDs)
            {
                if (!propertyColumn.ContainsKey(PropertyCodes.I_Real) || !propertyColumn.ContainsKey(PropertyCodes.U_Real))
                    continue;

                if (!propertyColumn[PropertyCodes.I_Real].ContainsKey(weldingMachineID) || !propertyColumn[PropertyCodes.U_Real].ContainsKey(weldingMachineID))
                    continue;

                var Icol = propertyColumn[PropertyCodes.I_Real][weldingMachineID];
                var Ucol = propertyColumn[PropertyCodes.U_Real][weldingMachineID];

                WeldingMachine machine = null;
                using (var __context = _weldingContextFactory.CreateContext(0))
                {
                    machine = __context.WeldingMachines.Find(weldingMachineID);
                }

                ExcelChart chart = worksheet_graph.Drawings.AddChart("chtLine", eChartType.Line);
                chart.Title.Text = machine.Name;
                chart.Legend.Position = eLegendPosition.Right;

                // Series data
                var data_I = worksheet.Cells[rowStart, Icol, rowStart + rowsTotal - 1, Icol];
                var data_U = worksheet.Cells[rowStart, Ucol, rowStart + rowsTotal - 1, Ucol];


                // Serie 1
                var serie1 = chart.Series.Add(data_U, range_titles) as ExcelLineChartSerie;
                serie1.Header = "Напряжение (U)";

                // Serie 2
                var chartType2 = chart.PlotArea.ChartTypes.Add(eChartType.Line);
                var serie2 = chartType2.Series.Add(data_I, range_titles) as ExcelLineChartSerie;
                serie2.Header = "Ток (I)";

                chart.UseSecondaryAxis = true;


                // 1 - left, 3 - right
                chart.Axis[1].Title.Text = "I";
                chart.Axis[1].Title.Rotation = 0;

                chart.Axis[3].Title.Text = "U";
                chart.Axis[3].Title.Rotation = 0;

                //chart.YAxis.Title.Text = "U";
                //chart.YAxis.Title.Rotation = 0;
                //chart.YAxis.Title.TextVertical = OfficeOpenXml.Drawing.eTextVerticalType.Horizontal;

                // Set size/position
                chart.SetSize(1100, 540);
                chart.SetPosition(600 * k + 80, 15);


                k++;
            }

            return worksheet_graph;
        }

        string translate(string PropertyCode, string value)
        {
            if (String.IsNullOrEmpty(value))
                return "";

            switch (PropertyCode)
            {
                case PropertyCodes.StateCtrl:
                    {
                        switch (value)
                        {
                            case "Free":
                                return "Без ограничений";
                            case "Passive":
                                return "Пассивный";
                            case "Limited":
                                return "Ограничения";
                            case "Block":
                                return "Блокировка";
                        }

                        break;
                    }
            }

            return value;
        }
    }
}
