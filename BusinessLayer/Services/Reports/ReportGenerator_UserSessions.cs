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
using BusinessLayer.Interfaces.Context;

namespace BusinessLayer.Services.Reports
{
    public class ReportGenerator_UserSessions : IReportGenerator
    {
        IWeldingContextFactory _weldingContextFactory;
        // WeldingContext _context;

        public ReportGenerator_UserSessions(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
        }

        public ReportGeneratorResult Generate(ReportRequest req)
        {
            byte[] fileContents;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sheet1");

                // Validate request
                if (!req.DateFrom.HasValue || !req.DateTo.HasValue)
                {
                    return null;
                }


                // Title
                worksheet.Cells[1, 1, 1, 5].Merge = true;
                worksheet.Cells[1, 1].Value = "Сессии пользователей";
                worksheet.Cells[1, 1].Style.Font.Size = 18;
                worksheet.Cells[1, 1].Style.Font.Bold = true;

                worksheet.Cells[2, 1, 2, 5].Merge = true;
                worksheet.Cells[2, 1].Value = String.Format("{0} - {1}", req.DateFrom.Value.ToString("dd-MM-yyyy"), req.DateTo.Value.ToString("dd-MM-yyyy"));

                worksheet.Column(1).Width = 10;
                worksheet.Column(2).Width = 30;
                worksheet.Column(3).Width = 30;
                worksheet.Column(4).Width = 30;
                worksheet.Column(5).Width = 30;

                using (var __context = _weldingContextFactory.CreateContext(18000))
                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required
                   , new TransactionOptions
                   {
                       IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted,
                       Timeout = TimeSpan.FromMinutes(15)
                   }))
                {
                    var dt_from = req.DateFrom.Value.Date;
                    var dt_to = req.DateTo.Value.Date;
                    var dt_to_next_day = dt_to.AddDays(1);

                    var sessions = __context.UserAccountSessions
                        .Include(s => s.UserAccount)
                        .Where(s => s.DateCreated >= dt_from && s.DateCreated < dt_to_next_day 
                            && s.UserAccount.OrganizationUnitID != null && req.OrganizationUnitIDs.Contains(s.UserAccount.OrganizationUnitID.Value));

                    // add UserAccountID
                    if (req.UserAccountID.GetValueOrDefault() > 0)
                        sessions = sessions.Where(s => s.UserAccountID == req.UserAccountID.Value);

                    // Order
                    sessions = sessions.OrderBy(s => s.DateCreated);

                    // Header Titles
                    var headerRow = 4;

                    worksheet.Row(headerRow).Style.Font.Bold = true;
                    worksheet.Cells[headerRow, 1].Value = "№";
                    worksheet.Cells[headerRow, 2].Value = "Пользователь";
                    worksheet.Cells[headerRow, 3].Value = "Начало сессии";
                    worksheet.Cells[headerRow, 4].Value = "Окончание";
                    worksheet.Cells[headerRow, 5].Value = "Длительность, чч:мм:сс";

                    var anyData = false;
                    var k = 0;
                    var row = headerRow + 1;
                    foreach (var s in sessions)
                    {
                        k++;

                        // #
                        worksheet.Cells[row, 1].Value = k.ToString();

                        // User
                        worksheet.Cells[row, 2].Value = s.UserAccount != null ? s.UserAccount.Name : "";

                        // Session datetime
                        worksheet.Cells[row, 3].Value = s.DateCreated.ToString("dd-MM-yyyy HH:mm:ss");
                        worksheet.Cells[row, 4].Value = s.DateUpdated.ToString("dd-MM-yyyy HH:mm:ss");

                        // Duration
                        worksheet.Cells[row, 5].Value = s.DateUpdated.Subtract(s.DateCreated);
                        worksheet.Cells[row, 5].Style.Numberformat.Format = "[h]:mm:ss";

                        anyData = true;
                        row++;
                    }

                    if (!anyData)
                    {
                        worksheet.Cells[row, 2].Value = "Нет данных за выбранный период";
                    }

                    scope.Complete();
                }



                // Finally when you're done, export it to byte array.
                fileContents = package.GetAsByteArray();
            }


            return new ReportGeneratorResult { ExcelData = fileContents };
        }

    }
}
