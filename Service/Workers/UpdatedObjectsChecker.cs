using BusinessLayer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Workers
{
    public class UpdatedObjectsChecker : ServiceHelpers.PeriodicWorker
    {
        DateTime? lastCheckOn;

        public UpdatedObjectsChecker(int periodSeconds) : base(periodSeconds)
        {
            Action = InternalCheck;

            // Load objects on initialization
            ReloadObjects();
        }

        protected void InternalCheck()
        {
            /*
            bool reload = false;

            // Check datetime updated
            using (var context = GetWeldingContext())
            {
                var update = context.ObjectUpdates
                    .Where(u => u.ObjectType == "*")
                    .OrderByDescending(u => u.DateCreated)
                    .Take(1)
                    .FirstOrDefault();

                if (update != null && update.DateCreated > lastCheckOn.Value)
                {
                    reload = true;
                }
            }

            // Reload objects
            if (reload)
            {
                ReloadObjects();
            }
            */

            ReloadObjects();
        }

        protected void ReloadObjects()
        {
            // Set the latest check-date
            lastCheckOn = DateTime.Now;

            // Fetch main data from db
            using (var context = GetWeldingContext())
            {
                // Organization Units
                Domain.DomainObjects.OrganizationUnits = new System.Collections.Concurrent.ConcurrentDictionary<int, DataLayer.Welding.OrganizationUnit>(
                    context.OrganizationUnits
                    .Where(i => i.Status != (int)GeneralStatus.Deleted)
                    .ToDictionary(i => i.ID)
                    );

                // Organization Units
                Domain.DomainObjects.NetworkDevices = new System.Collections.Concurrent.ConcurrentDictionary<int, DataLayer.Welding.NetworkDevice>(
                    context.NetworkDevices
                        .Where(i => i.Status != (int)GeneralStatus.Deleted)
                        .ToDictionary(i => i.ID)
                   );

                // Welding Machines
                Domain.DomainObjects.WeldingMachines = new System.Collections.Concurrent.ConcurrentDictionary<int, DataLayer.Welding.WeldingMachine>(
                    context.WeldingMachines
                        .Where(i => i.Status != (int)GeneralStatus.Deleted)
                        .ToDictionary(i => i.ID)
               );

                // Welding Machine Types
                Domain.DomainObjects.WeldingMachineTypes = new System.Collections.Concurrent.ConcurrentDictionary<int, DataLayer.Welding.WeldingMachineType>(
                    context.WeldingMachineTypes
                        .Where(i => i.Status != (int)GeneralStatus.Deleted)
                        .ToDictionary(i => i.ID)
                   );

            }

            // Build additional helper lists
            Domain.DomainObjects.BuildAdditionalObjects();
        }
    }
}
