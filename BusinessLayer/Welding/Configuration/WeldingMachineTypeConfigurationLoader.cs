using BusinessLayer.Interfaces.Context;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Configuration
{
    public class WeldingMachineTypeConfigurationLoader
    {
        WeldingContext _context;

        public WeldingMachineTypeConfigurationLoader(WeldingContext context)
        {
            _context = context;
        }

        public BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration LoadByMachine(int WeldingMachineID)
        {
            var weldingMachine = _context.WeldingMachines.Find(WeldingMachineID);
            if (weldingMachine == null)
                return null;

            return LoadByType(weldingMachine.WeldingMachineTypeID);
        }

        public BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration LoadByType(int WeldingMachineTypeID)
        {
            var weldingMachineType = _context.WeldingMachineTypes.Find(WeldingMachineTypeID);
            if (weldingMachineType == null)
                return null;

            BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration configuration;
            WeldingMachineTypeConfigurationLoader.TryParse(weldingMachineType.ConfigurationJSON, false, out configuration);

            return configuration;
        }

        static public bool ValidateJSON(string json, bool allowEmpty)
        {
            BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration configuration;
            return TryParse(json, allowEmpty, out configuration);
        }

        static public bool TryParse(string json, bool allowEmpty, out BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration configuration)
        {
            configuration = null;

            if (String.IsNullOrEmpty(json))
                return allowEmpty ? true : false;

            try
            {
                configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<BusinessLayer.Models.Configuration.WeldingMachineTypeConfiguration>(json);
            }
            catch (Exception ex)
            {
                return false;
            }

            // Set default values
            if (configuration.Settings == null)
                configuration.Settings = new Models.Configuration.Settings();

            // КПД аппарата
            if (configuration.Settings.Efficiency <= 0)
                configuration.Settings.Efficiency = 0.95;

            // Мощность при простое
            if (configuration.Settings.StandbyPower <= 0)
                configuration.Settings.StandbyPower = 20;

            return true;
        }
    }
}
