using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models
{
    public enum GeneralStatus
    {
        Active = 1,
        Inactive = 2,
        Deleted = 4
    }

    public enum WeldingMachineStatus
    {
        Off = 0,
        Ready = 1,
        Service = 2,
        Working = 3,
        Error = 4
    }

    public enum DetailAssemblyStatus
    {
        InProcess = 1,
        Completed = 2
    }

    public enum WeldingTaskStatus
    {
        InProcess = 1,
        OnControl = 2,
        Completed = 3
    }

    public enum QueueTaskStatus
    {
        New = 1,
        InProcess = 2,
        Processed = 3
    }

    public enum QueueTaskStatusResult
    {
        Ok = 1,
        Error = 2
    }

    public enum MaintenanceStatus
    {
        InProcess = 1,
        Completed = 2
    }
}
