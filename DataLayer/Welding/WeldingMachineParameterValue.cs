//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DataLayer.Welding
{
    using System;
    using System.Collections.Generic;
    
    public partial class WeldingMachineParameterValue
    {
        public int ID { get; set; }
        public int WeldingMachineStateID { get; set; }
        public string PropertyCode { get; set; }
        public string Value { get; set; }
        public string PropertyType { get; set; }
        public string RawValue { get; set; }
        public Nullable<bool> LimitsExceeded { get; set; }
        public string LimitMin { get; set; }
        public string LimitMax { get; set; }
    
        public virtual WeldingMachineState WeldingMachineState { get; set; }
    }
}
