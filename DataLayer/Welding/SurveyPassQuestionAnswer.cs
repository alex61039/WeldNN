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
    
    public partial class SurveyPassQuestionAnswer
    {
        public int ID { get; set; }
        public int SurveyPassID { get; set; }
        public int SurveyQuestionID { get; set; }
    
        public virtual SurveyPass SurveyPass { get; set; }
        public virtual SurveyQuestion SurveyQuestion { get; set; }
    }
}
