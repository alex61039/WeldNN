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
    
    public partial class Report_General_Result
    {
        public int ID { get; set; }
        public int WeldingMachineID { get; set; }
        public System.DateTime DateCreated { get; set; }
        public System.DateTime DateUpdated { get; set; }
        public Nullable<int> WeldingMachineStatus { get; set; }
        public int StateDurationMs { get; set; }
        public string ErrorCode { get; set; }
        public string ControlState { get; set; }
        public string WeldingMachineName { get; set; }
        public string WeldingMachineLabel { get; set; }
        public string WeldingMachineMAC { get; set; }
        public Nullable<int> UserAccountID { get; set; }
        public string UserAccountName { get; set; }
        public Nullable<int> OrganizationUnitID { get; set; }
        public string OrganizationUnitName { get; set; }
        public string Ireal { get; set; }
        public string Ureal { get; set; }
        public string Err { get; set; }
        public string WireSpeed { get; set; }
        public string GasFlow { get; set; }
        public string GasTotal { get; set; }
        public Nullable<int> WeldingMaterialID { get; set; }
    }
}
