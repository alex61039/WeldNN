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
    public class ReportGenerator_Graph : IReportGenerator
    {
        IWeldingContextFactory _weldingContextFactory;
        // WeldingContext _context;

        public ReportGenerator_Graph(IWeldingContextFactory weldingContextFactory)
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
            if (!req.Date.HasValue || !req.WeldingMachineID.HasValue)
            {
                return null;
            }

            WeldingMachine machine = null;
            using (var __context = _weldingContextFactory.CreateContext(0))
            {
                machine = __context.WeldingMachines.Find(req.WeldingMachineID.Value);
            }

            // Use Report_Params_Compare
            var report_compare = new ReportGenerator_Params_Compare(_weldingContextFactory);

            using (var package = new ExcelPackage())
            {
                var req2 = new ReportRequest
                {
                    Date = req.Date,
                    DateFrom = req.Date.Value,
                    DateTo = req.Date.Value,
                    TimeFrom = req.TimeFrom,
                    TimeTo = req.TimeTo,
                    WeldingMachineTypeID = machine.WeldingMachineTypeID,
                    WeldingMachineIDs = new List<int> { req.WeldingMachineID.Value },
                    PropertyCodes = new List<string> { PropertyCodes.I_Real, PropertyCodes.U_Real },
                    OrganizationUnitIDs = req.OrganizationUnitIDs
                };

                var worksheet = report_compare.BuildWorksheet(package, req2, buildGraph: true);

                // Finally when you're done, export it to byte array.
                fileContents = package.GetAsByteArray();
            }


            return new ReportGeneratorResult { ExcelData = fileContents };
        }

    }
}
