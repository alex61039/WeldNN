using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Configuration
{
    public static class ConfigurationExtension
    {
        public static string GetPropertyDescription(this WeldingMachineTypeConfiguration conf, string PropertyCode)
        {
            if (conf == null || String.IsNullOrEmpty(PropertyCode))
                return "";

            // Inbound.Body
            if (conf.Inbound != null && conf.Inbound.Body != null)
            {
                var p = conf.Inbound.Body.FirstOrDefault(pc => pc.PropertyCode == PropertyCode);
                if (p != null)
                    return p.Description;
            }

            // Inbound.Header
            if (conf.Inbound != null && conf.Inbound.Header != null)
            {
                var p = conf.Inbound.Header.FirstOrDefault(pc => pc.PropertyCode == PropertyCode);
                if (p != null)
                    return p.Description;
            }

            // Outbound.Body
            if (conf.Outbound != null && conf.Outbound.Body != null)
            {
                var p = conf.Outbound.Body.FirstOrDefault(pc => pc.PropertyCode == PropertyCode);
                if (p != null)
                    return p.Description;
            }

            // Outbound.Header
            if (conf.Outbound != null && conf.Outbound.Header != null)
            {
                var p = conf.Outbound.Header.FirstOrDefault(pc => pc.PropertyCode == PropertyCode);
                if (p != null)
                    return p.Description;
            }

            return "";
        }
    }
}