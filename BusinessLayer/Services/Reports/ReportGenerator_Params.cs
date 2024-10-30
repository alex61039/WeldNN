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
using BusinessLayer.Models.WeldingMachine;
using BusinessLayer.Interfaces.Context;

namespace BusinessLayer.Services.Reports
{
    public class ReportGenerator_Params : IReportGenerator
    {
        IWeldingContextFactory _weldingContextFactory;
        // WeldingContext _context;

        public ReportGenerator_Params(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
        }

        /// <summary>
        /// Parameters: DateFrom, DateTo, TimeFrom, TimeTo, WeldingMachineID, SplitBySeconds, PropertyCodes
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public ReportGeneratorResult Generate(ReportRequest req)
        {
            byte[] fileContents;


            // Validate request
            //if (!req.DateFrom.HasValue || !req.DateTo.HasValue || !req.WeldingMachineID.HasValue)
            //{
            //    return null;
            //}
            if (!req.Date.HasValue || !req.WeldingMachineID.HasValue)
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

        public ExcelWorksheet BuildWorksheet(ExcelPackage package, ReportRequest req)
        {
            var worksheet = package.Workbook.Worksheets.Add("Sheet1");


            // Retrieve machine/type/conf
            WeldingMachine weldingMachine = null;
            WeldingMachineTypeConfiguration conf = null;

            using (var _context = _weldingContextFactory.CreateContext(0))
            {
                weldingMachine = _context.WeldingMachines.Find(req.WeldingMachineID.Value);

                WeldingMachineTypeConfigurationLoader confLoader = new WeldingMachineTypeConfigurationLoader(_context);
                conf = confLoader.LoadByMachine(req.WeldingMachineID.Value);
            }


            var dt_from = req.Date.Value.Date;      // DateFrom
            var dt_to = req.Date.Value.Date;        // DateTo
            var dt_to_nextDay = req.Date.Value.Date.AddDays(1);

            TimeSpan? timeFrom = !String.IsNullOrEmpty(req.TimeFrom) ? TimeSpan.Parse(req.TimeFrom) : (TimeSpan?)null;
            TimeSpan? timeTo = !String.IsNullOrEmpty(req.TimeTo) ? TimeSpan.Parse(req.TimeTo) : (TimeSpan?)null;

            if (timeFrom.HasValue)
                dt_from = dt_from.Add(timeFrom.Value);
            if (timeTo.HasValue)
                dt_to = dt_to.Add(timeTo.Value);
            else
                dt_to = dt_to_nextDay;

            // Title
            worksheet.Cells[1, 1, 1, 5].Merge = true;
            worksheet.Cells[1, 1].Value = weldingMachine.Name;
            worksheet.Cells[1, 1].Style.Font.Size = 18;
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
            worksheet.Column(1).Width = 20;

            // Table header, properties list
            List<Models.WeldingMachine.SummaryProperty> summaryPropsAll = null;
            List<Models.WeldingMachine.SummaryProperty> summaryProps = null;
            using (var _context = _weldingContextFactory.CreateContext(0))
            {
                var panelStateBuilder = new PanelStateBuilder(conf, _context);
                summaryPropsAll = panelStateBuilder.BuildSummaryProperties(null, true);
                summaryProps = summaryPropsAll ?? new List<Models.WeldingMachine.SummaryProperty>();
                if (req.PropertyCodes != null && req.PropertyCodes.Count > 0)
                {
                    summaryProps = summaryPropsAll
                        .Where(p => req.PropertyCodes.Contains(p.PropertyCode))
                        .ToList();
                }
            }

            // Header Titles
            var headerRow = 4;

            // Duration
            if (req.SplitBy != "seconds")
            {
                worksheet.Column(2).Width = 15;
                worksheet.Cells[headerRow, 2].Value = "Длительность (чч:мм:сс)";
                worksheet.Cells[headerRow, 2].Style.Font.Bold = true;
                //worksheet.Cells[headerRow + 1, 2].Value = "чч:мм:сс";
                //worksheet.Cells[headerRow + 1, 2].Style.Font.Bold = true;
            }

            int _col = 0;
            var propertyColumn = new Dictionary<string, int>();
            var propertyDetails = new Dictionary<string, SummaryProperty>();
            foreach (var p in summaryProps)
            {
                worksheet.Column(3 + _col).Width = 15;
                worksheet.Cells[headerRow, 3 + _col].Value = p.Title + (String.IsNullOrEmpty(p.Unit) ? "" : (" (" + p.Unit + ")"));
                worksheet.Cells[headerRow, 3 + _col].Style.Font.Bold = true;
                //worksheet.Cells[headerRow + 1, 3 + col].Value = p.PropertyCode;
                //worksheet.Cells[headerRow + 1, 3 + col].Style.Font.Bold = true;

                propertyColumn[p.PropertyCode] = _col;
                propertyDetails[p.PropertyCode] = p;
                _col++;
            }

            // DATA
            using (var _context = _weldingContextFactory.CreateContext(18000))
            using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required
               , new TransactionOptions
               {
                   IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted,
                   Timeout = TimeSpan.FromMinutes(15)
               }))
            {
                //var stateIDs = _context.WeldingMachineStates
                //.Where(s =>
                //    s.WeldingMachineID == req.WeldingMachineID.Value
                //    && s.DateCreated >= dt_from
                //    && s.DateCreated < dt_to_nextDay
                //)
                //.OrderBy(s => s.DateCreated)
                //.Select(s => s.ID)
                //.ToArray();

                var propsArr = summaryProps.Select(p => p.PropertyCode).ToArray();

                var data = _context.vStatePropValues
                    .Where(s =>
                            s.WeldingMachineID == req.WeldingMachineID.Value
                            && s.DateCreated >= dt_from
                            && s.DateCreated < dt_to
                            && propsArr.Contains(s.PropertyCode)
                    )
                    .OrderBy(s => s.DateCreated)
                    .ThenBy(s => s.ID);


                var row = headerRow;
                int prev_stateID = 0;
                TimeSpan prev_timespan = new TimeSpan();
                DateTime prev_row_dt = DateTime.Now;
                foreach (var item in data)
                {
                    // Check that machine is ON
                    var weldingMachineStatus = (WeldingMachineStatus)item.WeldingMachineStatus.GetValueOrDefault();
                    if (weldingMachineStatus != WeldingMachineStatus.Ready 
                        && weldingMachineStatus != WeldingMachineStatus.Working 
                        && weldingMachineStatus != WeldingMachineStatus.Error)
                        continue;



                    // DATA - by Seconds?
                    if (prev_stateID != item.ID)
                    {
                        // Split by seconds - duplicate row(s)
                        if (req.SplitBy == "seconds" && prev_stateID > 0)
                        {
                            do
                            {
                                // Less than a second?
                                if (prev_timespan.TotalMilliseconds <= 1000)
                                    break;

                                // Subtract 1 second
                                prev_timespan = prev_timespan.Subtract(TimeSpan.FromSeconds(1));

                                // Same second?
                                if (prev_timespan.TotalMilliseconds < 1000 && prev_row_dt.Add(prev_timespan).Second == prev_row_dt.Second)
                                {
                                    break;
                                }


                                // Add 1 second
                                prev_row_dt = prev_row_dt.AddSeconds(1);

                                // Duplicate rows
                                row++;
                                worksheet.Cells[row, 1].Value = prev_row_dt.ToString("dd-MM-yyyy HH:mm:ss");
                                worksheet.Cells[row, 2].Value = TimeSpan.FromSeconds(1);
                                worksheet.Cells[row, 2].Style.Numberformat.Format = "[h]:mm:ss";

                                for (var i = 0; i < summaryProps.Count; i++)
                                {
                                    worksheet.Cells[row, 3 + i].Value = worksheet.Cells[row - 1, 3 + i].Value;

                                }
                            } while (true);
                        }

                        // New row
                        row++;

                        // Date time
                        worksheet.Cells[row, 1].Value = item.DateCreated.ToString("dd-MM-yyyy HH:mm:ss");

                        // Duration
                        worksheet.Cells[row, 2].Value = req.SplitBy == "seconds" ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(item.StateDurationMs);
                        worksheet.Cells[row, 2].Style.Numberformat.Format = "[h]:mm:ss";

                        prev_stateID = item.ID;
                        prev_row_dt = item.DateCreated;
                        prev_timespan = TimeSpan.FromMilliseconds(item.StateDurationMs);
                    }

                    // Properties
                    if (propsArr.Contains(item.PropertyCode))
                    {
                        var propDetails = propertyDetails.ContainsKey(item.PropertyCode) ? propertyDetails[item.PropertyCode] : new SummaryProperty();

                        // find a col
                        int col = 0;
                        if (propertyColumn.ContainsKey(item.PropertyCode))
                        {
                            col = propertyColumn[item.PropertyCode];
                        }
                        else
                        {
                            // New property
                            col = propertyColumn.Count;
                            propertyColumn[item.PropertyCode] = col;

                            // Add to header
                            worksheet.Column(3 + col).Width = 15;
                            worksheet.Cells[headerRow, 3 + col].Value = propDetails.Title + (String.IsNullOrEmpty(propDetails.Unit) ? "" : (" (" + propDetails.Unit + ")"));
                            worksheet.Cells[headerRow, 3 + col].Style.Font.Bold = true;
                            worksheet.Cells[headerRow + 1, 3 + col].Value = propDetails.PropertyCode;
                            worksheet.Cells[headerRow + 1, 3 + col].Style.Font.Bold = true;
                        }

                        // VALUE
                        if (item.PropertyType == "number")
                        {
                            if (double.TryParse(item.Value, out double d))
                                worksheet.Cells[row, 3 + col].Value = d;
                            else
                                worksheet.Cells[row, 3 + col].Value = item.Value;
                        }
                        else
                        {
                            worksheet.Cells[row, 3 + col].Value = translate(propDetails.PropertyCode, item.Value);
                        }
                    }


                    /*
                    do
                    {
                        if (req.SplitBy == "seconds")
                        {
                            // Less than a second?
                            if (timespan.TotalMilliseconds <= 1000)
                                break;

                            // Subtract 1 second
                            timespan = timespan.Subtract(TimeSpan.FromSeconds(1));

                            // Same second?
                            if (timespan.TotalMilliseconds < 1000 && row_dt.Add(timespan).Second == row_dt.Second)
                            {
                                break;
                            }

                            // Add 1 second
                            row_dt = row_dt.AddSeconds(1);
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (true);
                    */
                }


                scope.Complete();
            }

            return worksheet;
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
