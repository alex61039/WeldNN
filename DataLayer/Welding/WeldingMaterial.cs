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
    
    public partial class WeldingMaterial
    {
        public int ID { get; set; }
        public int WeldingMaterialTypeID { get; set; }
        public int Status { get; set; }
        public System.DateTime DateCreated { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Description { get; set; }
        public Nullable<double> Diameter_mm { get; set; }
        public Nullable<double> WeightBlock_kg { get; set; }
        public Nullable<double> LengthBlock_m { get; set; }
        public Nullable<double> WeightPerMeter_kg { get; set; }
        public string Composition { get; set; }
        public Nullable<double> LengthItem_mm { get; set; }
        public Nullable<double> WeightItem_kg { get; set; }
        public Nullable<double> QuantityInBlock { get; set; }
        public string Category { get; set; }
        public string Sizes { get; set; }
        public Nullable<double> Thickness_mm { get; set; }
        public Nullable<double> k0 { get; set; }
        public Nullable<double> k1 { get; set; }
        public Nullable<double> k2 { get; set; }
        public Nullable<double> limit_upper { get; set; }
        public Nullable<double> limit_lower { get; set; }
    
        public virtual WeldingMaterialType WeldingMaterialType { get; set; }
    }
}