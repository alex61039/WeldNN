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
    public class ReportGenerator_Diagram : IReportGenerator
    {
        IWeldingContextFactory _weldingContextFactory;
        // WeldingContext _context;

        public ReportGenerator_Diagram(IWeldingContextFactory weldingContextFactory)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
        }

        /// <summary>
        /// Parameters: Date, TimeFrom?, TimeTo?, WeldingMachineID
        /// </summary>
        /// <returns></returns>
        public ReportGeneratorResult Generate(ReportRequest req)
        {
            byte[] fileContents;

            // Validate request


            // Use Report_General
            var report_general = new ReportGenerator_General(_weldingContextFactory);

            using (var package = new ExcelPackage())
            {
                var req2 = new ReportRequest
                {
                    DateFrom = req.DateFrom,
                    DateTo = req.DateTo,
                    TimeFrom = req.TimeFrom,
                    TimeTo = req.TimeTo,
                    UserAccountID = req.UserAccountID,
                    OrganizationUnitID = req.OrganizationUnitID,
                    WeldingMachineID = req.WeldingMachineID,
                    OrganizationUnitIDs = req.OrganizationUnitIDs
                };

                var worksheet = report_general.BuildWorksheet(package, req2, buildGraph: true);

                // Finally when you're done, export it to byte array.
                fileContents = package.GetAsByteArray();
            }


            return new ReportGeneratorResult { ExcelData = fileContents };
        }

    }
}
