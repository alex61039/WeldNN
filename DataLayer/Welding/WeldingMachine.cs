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
    
    public partial class WeldingMachine
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public WeldingMachine()
        {
            this.Maintenances = new HashSet<Maintenance>();
            this.WeldingLimitPrograms = new HashSet<WeldingLimitProgram>();
            this.WeldingMachineStates = new HashSet<WeldingMachineState>();
        }
    
        public int ID { get; set; }
        public int OrganizationUnitID { get; set; }
        public int WeldingMachineTypeID { get; set; }
        public int Status { get; set; }
        public System.DateTime DateCreated { get; set; }
        public string Name { get; set; }
        public string MAC { get; set; }
        public string SerialNumber { get; set; }
        public string YearManufactured { get; set; }
        public Nullable<System.DateTime> DateStartedUsing { get; set; }
        public string InventoryNumber { get; set; }
        public Nullable<int> MaintenanceRegulation { get; set; }
        public Nullable<double> MaintenanceInterval { get; set; }
        public string Modules { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public Nullable<double> PlanPositionX { get; set; }
        public Nullable<double> PlanPositionY { get; set; }
        public Nullable<System.DateTime> LastServiceOn { get; set; }
        public Nullable<long> TimeTotalSecs { get; set; }
        public Nullable<long> TimeAfterLastServiceSecs { get; set; }
        public Nullable<int> UserServiceNotifiedBeforeHours { get; set; }
        public Nullable<System.DateTime> UserServiceNotifiedOn { get; set; }
        public Nullable<long> TimeTillNextServiceSecs { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Maintenance> Maintenances { get; set; }
        public virtual OrganizationUnit OrganizationUnit { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<WeldingLimitProgram> WeldingLimitPrograms { get; set; }
        public virtual WeldingMachineType WeldingMachineType { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<WeldingMachineState> WeldingMachineStates { get; set; }
    }
}