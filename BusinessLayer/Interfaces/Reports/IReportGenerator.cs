using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Interfaces.Reports
{
    public class ReportGeneratorResult
    {
        public byte[] ExcelData { get; set; }

        public string JSON { get; set; }
    }

    public interface IReportGenerator
    {
        ReportGeneratorResult Generate(Models.ReportRequest req);
    }
}
