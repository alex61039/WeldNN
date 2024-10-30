using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace WeldingService.Domain
{
    public class DomainObjects
    {
        static public ConcurrentDictionary<int, DataLayer.Welding.OrganizationUnit> OrganizationUnits = new ConcurrentDictionary<int, DataLayer.Welding.OrganizationUnit>();
        static public ConcurrentDictionary<int, DataLayer.Welding.NetworkDevice> NetworkDevices = new ConcurrentDictionary<int, DataLayer.Welding.NetworkDevice>();
        static public ConcurrentDictionary<int, DataLayer.Welding.WeldingMachine> WeldingMachines = new ConcurrentDictionary<int, DataLayer.Welding.WeldingMachine>();
        static public ConcurrentDictionary<int, DataLayer.Welding.WeldingMachineType> WeldingMachineTypes = new ConcurrentDictionary<int, DataLayer.Welding.WeldingMachineType>();

        static public ConcurrentDictionary<string, DataLayer.Welding.WeldingMachine> WeldingMachinesByMAC = new ConcurrentDictionary<string, DataLayer.Welding.WeldingMachine>();
        static public ConcurrentDictionary<int, BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration> ConfigsByTypeID = new ConcurrentDictionary<int, BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration>();
        static public ConcurrentDictionary<string, BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration> ConfigsByMAC = new ConcurrentDictionary<string, BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration>();

        static public void BuildAdditionalObjects()
        {
            // WeldingMachines by MAC
            lock (WeldingMachinesByMAC)
            {
                WeldingMachinesByMAC.Clear();

                foreach (var m in WeldingMachines.Values)
                {
                    if (!String.IsNullOrEmpty(m.MAC))
                    {
                        WeldingMachinesByMAC[m.MAC] = m;
                    }
                }
            }

            // Configs by TypeID
            lock (ConfigsByTypeID)
            {
                ConfigsByTypeID.Clear();

                foreach(var t in WeldingMachineTypes.Values)
                {
                    if (!String.IsNullOrWhiteSpace(t.ConfigurationJSON))
                    {
                        // Try parse config
                        BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration config;

                        if (BusinessLayer.Welding.Configuration.WeldingMachineTypeConfigurationLoader.TryParse(
                            t.ConfigurationJSON,
                            false,
                            out config
                            ))
                        {
                            if (config != null)
                            {
                                // Store the config
                                ConfigsByTypeID[t.ID] = config;
                            }
                        }
                    }
                }
            }

            // Configs by MAC
            lock (ConfigsByMAC)
            {
                ConfigsByMAC.Clear();

                foreach(var m in WeldingMachines.Values)
                {
                    if (!String.IsNullOrEmpty(m.MAC) && ConfigsByTypeID.ContainsKey(m.WeldingMachineTypeID))
                    {
                        ConfigsByMAC[m.MAC] = ConfigsByTypeID[m.WeldingMachineTypeID];
                    }
                }
            }
        }
    }
}
