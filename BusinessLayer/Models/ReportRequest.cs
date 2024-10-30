using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models
{
    public class ReportRequest
    {
        public string ReportType { get; set; }
        public string ReportName { get; set; }

        public string Lang { get; set; }

        public DateTime? Date { get; set; }

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public string TimeFrom { get; set; }
        public string TimeTo { get; set; }

        public int? UserAccountID { get; set; }

        public int? OrganizationUnitID { get; set; }

        public int? WeldingMachineID { get; set; }
        public int? WeldingMachineTypeID { get; set; }

        public ICollection<int> WeldingMachineIDs { get; set; }

        /// <summary>
        /// Filled depending on user's permissions/organization
        /// </summary>
        public ICollection<int> OrganizationUnitIDs { get; set; }

        /// <summary>
        /// Time, or period
        /// e.g. 'seconds' for ReportParams
        /// </summary>
        public string SplitBy { get; set; }

        /// <summary>
        /// Набор параметров для отчета по параметрам
        /// </summary>
        public ICollection<String> PropertyCodes { get; set; }
    }
}
