using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;

namespace DataLayer.Welding
{
    public partial class WeldingContext : DbContext
    {
        public WeldingContext(string connectionString, bool LazyLoadingEnabled) : base(connectionString)
        {
            this.Configuration.LazyLoadingEnabled = LazyLoadingEnabled;
            this.Configuration.ProxyCreationEnabled = false;
        }
    }
}
