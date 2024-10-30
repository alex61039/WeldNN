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

namespace BusinessLayer.Services.Reports
{
    public class ReportGeneratorBase
    {
        WeldingContext _context;

        public ReportGeneratorBase(WeldingContext context)
        {
            _context = context;
        }
    }
}
