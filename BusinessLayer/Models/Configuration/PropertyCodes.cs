using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Models.Configuration
{
    public class PropertyCodes
    {
        /// <summary>
        /// CRC-8 code
        /// </summary>
        public const string CRC8 = "CRC";

        /// <summary>
        /// MAC-address
        /// </summary>
        public const string MACAddress = "MACAddress";

        /// <summary>
        /// Дата сервера на аппарат для синхронизации
        /// Server datetime
        /// </summary>
        public const string ServerDatetime = "Server.Datetime";

        /// <summary>
        /// Входящая дата с аппарата
        /// Machine datetime
        /// </summary>
        public const string MachineDatetime = "State.Datetime";

        /// <summary>
        /// Machine Model
        /// </summary>
        // public const string Model = "MODEL";

        /// <summary>
        /// Machine Version
        /// </summary>
        // public const string Version = "VERSION";

        /// <summary>
        /// Wire diameter
        /// </summary>
        // public const string WireDiameter = "State.wireDiam";

        /// <summary>
        /// Single parameter on panel
        /// </summary>
        // public const string StateParm = "State.parm";

        /// <summary>
        /// Left parameter on panel
        /// </summary>
        // public const string StateParmL = "State.parmL";

        /// <summary>
        /// Right parameter on panel
        /// </summary>
        // public const string StateParmR = "State.parmR";

        /// <summary>
        /// Machine state (MP)
        /// </summary>
        public const string State = "State.state";

        /// <summary>
        /// Machine state (MX)
        /// </summary>
        public const string StateFlags = "State.sFlags";

        /// <summary>
        /// Error code for MP
        /// </summary>
        public const string StateError = "State.errors";


        /// <summary>
        /// Error code for MX
        /// </summary>
        public const string StateErr = "State.errFlags";



        /// <summary>
        /// Machine Mode (free, limited, blocked, etc)
        /// </summary>
        public const string StateCtrl = "State.ctrl";

        /// <summary>
        /// Program number
        /// </summary>
        // public const string StatePrg = "State.Prg";

        /// <summary>
        /// Current (выставленный ток)
        /// </summary>
        // public const string I = "State.I";

        /// <summary>
        /// Voltage (выставленное напряжение)
        /// </summary>
        // public const string U = "State.U";

        /// <summary>
        /// Current (фактический ток)
        /// </summary>
        public const string I_Real = "State.Ireal";

        /// <summary>
        /// Voltage (фактическое напряжение)
        /// </summary>
        public const string U_Real = "State.Ureal";

        /// <summary>
        /// Inductance
        /// </summary>
        // public const string L = "State.L";

        /// <summary>
        /// Cathet size
        /// </summary>
        // public const string K = "State.K";

        /// <summary>
        /// Wire Speed
        /// </summary>
        // public const string WS = "State.WS";

        /// <summary>
        /// Metal thickness
        /// </summary>
        // public const string T = "State.T";

        /// <summary>
        /// Machine robot speed
        /// </summary>
        // public const string S = "State.S";

        /// <summary>
        /// Arc elasticity
        /// </summary>
        // public const string ArcElast = "State.PAR6";

        /// <summary>
        /// RFID
        /// </summary>
        public const string RFID = "rfid";

        /// <summary>
        /// Single controlled param
        /// </summary>
        public const string CtrlParm = "Ctrl.parm";

        /// <summary>
        /// Left controlled param
        /// </summary>
        public const string CtrlParmL = "Ctrl.parmL";

        /// <summary>
        /// Right controlled param
        /// </summary>
        public const string CtrlParmR = "Ctrl.parmR";

        /// <summary>
        /// Single controlled param - min value
        /// </summary>
        public const string CtrlMin = "Ctrl.min";

        /// <summary>
        /// Single controlled param - max value
        /// </summary>
        public const string CtrlMax = "Ctrl.max";

        /// <summary>
        /// Left controlled param - min value
        /// </summary>
        public const string CtrlMinL = "Ctrl.minL";

        /// <summary>
        /// Left controlled param - max value
        /// </summary>
        public const string CtrlMaxL = "Ctrl.maxL";

        /// <summary>
        /// Right controlled param - min value
        /// </summary>
        public const string CtrlMinR = "Ctrl.minR";

        /// <summary>
        /// Right controlled param - max value
        /// </summary>
        public const string CtrlMaxR = "Ctrl.maxR";

        /// <summary>
        /// Barcode
        /// </summary>
        public const string Barcode = "scan";

        /// <summary>
        /// Counter for offline messages
        /// </summary>
        public const string PCount = "State.Pcount";

        /// <summary>
        /// Used in report to calculate Wire flow (in report stored procedure)
        /// </summary>
        public const string StateWireSpeed = "State.WireSpeed";

        /// <summary>
        /// Used in report to calculate flow (in report stored procedure)
        /// </summary>
        public const string StateGasFlow = "State.GasFlow";

        /// <summary>
        /// Used in report to calculate flow (in report stored procedure)
        /// </summary>
        public const string StateGasTotal = "State.GasTotal";
    }
}
