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
    
    public partial class DetailAssembly
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DetailAssembly()
        {
            this.WeldingAssemblyControlResults = new HashSet<WeldingAssemblyControlResult>();
            this.WeldingDetailAssemblyTasks = new HashSet<WeldingDetailAssemblyTask>();
            this.DetailParts = new HashSet<DetailPart>();
        }
    
        public int ID { get; set; }
        public int DetailAssemblyTypeID { get; set; }
        public int Status { get; set; }
        public System.DateTime DateCreated { get; set; }
        public string SerialNumber { get; set; }
        public string Description { get; set; }
        public Nullable<int> DetailAssemblyStatus { get; set; }
    
        public virtual DetailAssemblyType DetailAssemblyType { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<WeldingAssemblyControlResult> WeldingAssemblyControlResults { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<WeldingDetailAssemblyTask> WeldingDetailAssemblyTasks { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<DetailPart> DetailParts { get; set; }
    }
}
