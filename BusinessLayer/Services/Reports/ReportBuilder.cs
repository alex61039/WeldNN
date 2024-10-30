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
    public class ReportBuildResult
    {
        public Guid? DocumentGUID { get; set; }
        public string ErrorMessage { get; set; }
        public string JSON { get; set; }
    }

    public class ReportBuilder
    {
        // WeldingContext _context;
        Configuration.StorageOptions _storageOptions;
        IWeldingContextFactory _weldingContextFactory;

        public ReportBuilder(IWeldingContextFactory weldingContextFactory, Configuration.StorageOptions storageOptions)
        {
            // _context = context;
            _weldingContextFactory = weldingContextFactory;
            _storageOptions = storageOptions;
        }

        public ReportBuildResult Build(int UserAccountID, ReportRequest req)
        {
            // Apply user's Organizations
            req = ApplyUsersOrganizationsPermissions(UserAccountID, req);

            var storageOptions = Microsoft.Extensions.Options.Options.Create(_storageOptions);

            Document doc = null;

            var generateReportResult = GenerateReport(req);

            // Excel?
            if (generateReportResult?.ExcelData != null)
            {
                var stream = new MemoryStream(generateReportResult.ExcelData);

                var filename = String.Format("{0}.xlsx",
                    String.IsNullOrEmpty(req.ReportName) ? req.ReportType : req.ReportName);

                using (var _context = _weldingContextFactory.CreateContext(0))
                {
                    var documentsService = new DocumentsService(_context, storageOptions);

                    doc = documentsService.AddDocument(
                        stream,
                        "application/vnd.ms-excel",
                        filename,
                        req.UserAccountID
                        );
                }
            }


            var result = new ReportBuildResult
            {
                ErrorMessage = null,
                DocumentGUID = doc?.GUID,
                JSON = generateReportResult.JSON
            };

            return result;
        }

        private ReportRequest ApplyUsersOrganizationsPermissions(int UserAccountID, ReportRequest req)
        {
            req.OrganizationUnitIDs = new List<int>();

            using (var _context = _weldingContextFactory.CreateContext(0))
            {
                var accountsManager = new Accounts.AccountsManager(_context);


                var query = _context.OrganizationUnits.Where(m => m.Status == (int)GeneralStatus.Active);

                // Check it not super-admin, and has no permission 'ManageAllOrganizations'
                var userAccount = accountsManager.GetUserAccountWithRoles(UserAccountID);
                if (userAccount.OrganizationUnitID.HasValue && !accountsManager.HasAccess(userAccount, "ManageAllOrganizations", UserPermissionAccess.Read))
                {
                    var organizationID = _context.OrganizationUnits.Find(userAccount.OrganizationUnitID.Value).OrganizationID;

                    // Filter by user's OrganizationID
                    query = query.Where(m => m.OrganizationID == organizationID);
                }

                // Select only OrganizationUnitIDs
                req.OrganizationUnitIDs = query.Select(o => o.ID).ToList();
            }

            return req;
        }

        private ReportGeneratorResult GenerateReport(ReportRequest req)
        {
            using (var _context = _weldingContextFactory.CreateContext(18000))
            {
                Interfaces.Reports.IReportGenerator reportGenerator = null;

                switch (req.ReportType)
                {
                    case "report_general":
                        reportGenerator = new ReportGenerator_General(_weldingContextFactory);
                        break;

                    case "report_params":
                        reportGenerator = new ReportGenerator_Params(_weldingContextFactory);
                        break;

                    case "report_params_compare":
                        reportGenerator = new ReportGenerator_Params_Compare(_weldingContextFactory);
                        break;

                    case "report_user_sessions":
                        reportGenerator = new ReportGenerator_UserSessions(_weldingContextFactory);
                        break;

                    case "report_graph":
                        reportGenerator = new ReportGenerator_Graph(_weldingContextFactory);
                        break;

                    case "report_diagram":
                        reportGenerator = new ReportGenerator_Diagram(_weldingContextFactory);
                        break;

                    case "report_errors":
                        reportGenerator = new ReportGenerator_Errors(_weldingContextFactory);
                        break;

                    case "report_passive_limits":
                        reportGenerator = new ReportGenerator_Passive_Limits(_weldingContextFactory);
                        break;

                    case "report_maintenance":
                        reportGenerator = new ReportGenerator_Maintenance(_weldingContextFactory);
                        break;

                    case "report_timeline":
                        reportGenerator = new ReportGenerator_Timeline(_weldingContextFactory);
                        break;

                }

                ReportGeneratorResult result = reportGenerator.Generate(req);

                return result;
            }
        }

    }
}
