using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    public interface IInstallationStep
    {
        string GetName();

        bool Process();
    }
}
