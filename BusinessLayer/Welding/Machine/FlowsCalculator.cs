using BusinessLayer.Interfaces.Context;
using BusinessLayer.Models;
using BusinessLayer.Models.Configuration;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Machine
{
    public class FlowsCalculator
    {
        // WeldingContext _context;
        IWeldingContextFactory _weldingContextFactory;

        public FlowsCalculator(IWeldingContextFactory weldingContextFactory)
        {
            _weldingContextFactory = weldingContextFactory;
            // _context = context;
        }


        struct GasTotal
        {
            public DateTime LastStateDatetime;
            public double LastGasTotal;
        }
        Dictionary<int, GasTotal> dictGasTotalByMachine;

        /// <summary>
        /// Расход газа по счетчику с аппарата
        /// </summary>
        /// <returns>В литрах</returns>
        public double CalculateGasFlow(Report_General_Result d)
        {
            WeldingMachineStatus status = (WeldingMachineStatus)d.WeldingMachineStatus;

            // только в режиме сварки
            if (status != WeldingMachineStatus.Working)
                return 0;


            // By GasTotal or GasFlow:
            // GasFlow - расход в 0,1 л/мин
            // GasTotal - несбрасываемый счетчик в литрах

            // По расходу надо брать что больше - расход умноженный на время, либо разница между показаниями счетчика.
            double byGasFlow = 0;
            double byGasTotal = 0;

            // GasFlow
            if (!String.IsNullOrEmpty(d.GasFlow) && Double.TryParse(d.GasFlow, out double f) && f > 0)
            {
                // минуты -> миллисекунды
                byGasFlow = f / 10.0 * ((double)d.StateDurationMs / 1000.0 / 60.0);
            }

            // GasTotal
            if (!String.IsNullOrEmpty(d.GasTotal) && Double.TryParse(d.GasTotal, out double t) && t > 0)
            {
                if (dictGasTotalByMachine == null) dictGasTotalByMachine = new Dictionary<int, GasTotal>();

                if (dictGasTotalByMachine.ContainsKey(d.WeldingMachineID))
                {
                    var item = dictGasTotalByMachine[d.WeldingMachineID];

                    // Рядом?
                    if (d.DateCreated.Subtract(item.LastStateDatetime).TotalMilliseconds <= 2000)
                    {
                        byGasTotal = t - item.LastGasTotal;
                        if (byGasTotal < 0) byGasTotal = 0;
                    }

                }

                // сохранить последнее/текущее значение
                dictGasTotalByMachine.Add(d.WeldingMachineID, new GasTotal { LastStateDatetime = d.DateCreated, LastGasTotal = t });
            }

            return Math.Max(byGasFlow, byGasTotal);
        }

        struct Wire
        {
            public bool Valid;

            /// <summary>
            /// погонная плотность проволоки, кг*м
            /// </summary>
            public double P;

            public double k0;
            public double k1;
            public double k2;

            public double? limit_upper;
            public double? limit_lower;
        }
        Dictionary<int, Wire> dictWires;
        Wire getWire(int WeldingMaterialID)
        {
            if (dictWires == null)
                dictWires = new Dictionary<int, Wire>();

            if (dictWires.ContainsKey(WeldingMaterialID))
                return dictWires[WeldingMaterialID];

            var wire = new Wire { Valid = false };

            // Load from db
            // Только проволока
            using (var _context = _weldingContextFactory.CreateContext(0))
            {
                var material = _context.WeldingMaterials.FirstOrDefault(m => m.ID == WeldingMaterialID);
                if (material == null || material.WeldingMaterialTypeID != (int)WeldingMaterialTypeEnum.Wire)
                    return wire;

                // Валидна?
                if (material.WeightPerMeter_kg.HasValue && material.WeightPerMeter_kg.Value > 0)
                {
                    wire.Valid = true;
                    wire.P = material.WeightPerMeter_kg.Value;
                    wire.k0 = material.k0.GetValueOrDefault();
                    wire.k1 = material.k1.GetValueOrDefault();
                    wire.k2 = material.k2.GetValueOrDefault();
                    wire.limit_lower = material.limit_lower;
                    wire.limit_upper = material.limit_upper;
                }
            }

            dictWires[WeldingMaterialID] = wire;

            return wire;
        }

        /// <summary>
        /// Wire/Расход прволоки (кг/мин)
        /// только в режиме сварки
        /// </summary>
        /// <param name="d"></param>
        /// <returns>кг/мин</returns>
        public double CalculateWireFlow(Report_General_Result d)
        {
            WeldingMachineStatus status = (WeldingMachineStatus)d.WeldingMachineStatus;

            // Только в режиме сварки
            if (status != WeldingMachineStatus.Working || d.WeldingMaterialID.GetValueOrDefault() <= 0)
                return 0;


            double result = 0;

            double Ireal = 0;
            if (!Double.TryParse(d.Ireal, out Ireal)) Ireal = 0;


            var wire = getWire(d.WeldingMaterialID.Value);
            if (wire.Valid)
            {
                // Расчитать скорость проволоки
                double wireSpeed = 0;

                // Расчет либо по формуле с коэффициентами, либо по скорости проволоки от аппарата
                if (!Double.TryParse(d.WireSpeed, out wireSpeed) || wireSpeed <= 0)
                {
                    // (k0 + k1*Ireal + k2*Ireal*Ireal )*P*T
                    // k0,k1,k2 - коэффициенты для каждого диаметра и типа проволоки
                    wireSpeed = (wire.k0 + wire.k1 * Ireal + wire.k2 * Ireal * Ireal);

                    // проверить лимиты
                    if (wire.limit_lower.HasValue && wireSpeed < wire.limit_lower.Value)
                        wireSpeed = wire.limit_lower.Value;

                    if (wire.limit_upper.HasValue && wireSpeed > wire.limit_upper.Value)
                        wireSpeed = wire.limit_upper.Value;
                }

                // Р - погонная плотность проволоки, кг/м
                // Т - время горения дуги (мин)
                // State.WireSpeed - 0.1 м/мин
                // result = wireSpeed / 10.0 * wire.P * ((double)d.StateDurationMs / 1000.0 / 60.0);
                result = wireSpeed * wire.P * ((double)d.StateDurationMs / 1000.0 / 60.0);
            }

            return result;
        }

        public double CalculateElectricity(WeldingMachineTypeConfiguration config, Report_General_Result d)
        {
            WeldingMachineStatus status = (WeldingMachineStatus)d.WeldingMachineStatus;

            double Ireal = 0, Ureal = 0;
            if (!Double.TryParse(d.Ireal, out Ireal)) Ireal = 0;
            if (!Double.TryParse(d.Ureal, out Ureal)) Ureal = 0;

            double kpd = 0.95;      // КПД аппарата (по умолчанию)
            double p_standby = 0.020; // Потребляемая мощность при простое (20 Вт)  (по умолчанию)

            if (config != null)
            {
                kpd = config.Settings.Efficiency;
                p_standby = config.Settings.StandbyPower / 1000; // StandbyPower - в Ваттах
            }


            return status == WeldingMachineStatus.Working ? (Ureal * Ireal / kpd * (double)d.StateDurationMs / 1000.0 / 3600.0 / 1000.0)  // кВт*ч
                        : status == WeldingMachineStatus.Ready ? (p_standby * (double)d.StateDurationMs / 1000.0 / 3600.0)      // кВт*ч (уже в киловаттах)
                        : 0;
        }
    }
}
